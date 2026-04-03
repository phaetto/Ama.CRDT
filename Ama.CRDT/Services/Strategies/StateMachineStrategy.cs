namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using System;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;
using System.Collections.Generic;
using Ama.CRDT.Extensions;

/// <summary>
/// Implements a State Machine strategy. This strategy enforces valid state transitions
/// while using a Last-Writer-Wins (LWW) mechanism for conflict resolution among valid transitions.
/// </summary>
[CrdtSupportedType(typeof(object))]
[CrdtSupportedIntent(typeof(SetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class StateMachineStrategy(
    ReplicaContext replicaContext, 
    IServiceProvider serviceProvider, 
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        if (Equals(originalValue, modifiedValue))
        {
            return;
        }

        var attribute = property.StrategyAttribute as CrdtStateMachineStrategyAttribute;
        if (attribute is null) return;

        if (!IsValidTransition(attribute.ValidatorType, property.PropertyType, originalValue, modifiedValue))
        {
            return;
        }
        
        if (originalMeta.Lww.TryGetValue(path, out var originalTimestamp) && originalTimestamp.Timestamp is not null && changeTimestamp.CompareTo(originalTimestamp.Timestamp) <= 0)
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp, clock);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (root, _, path, property, intent, timestamp, clock) = context;

        if (intent is not SetIntent setIntent)
        {
            throw new NotSupportedException($"Intent {intent.GetType().Name} is not supported by {nameof(StateMachineStrategy)}.");
        }

        var attribute = property.StrategyAttribute as CrdtStateMachineStrategyAttribute;
        if (attribute is null)
        {
            throw new InvalidOperationException($"Property {property.Name} is missing the {nameof(CrdtStateMachineStrategyAttribute)}.");
        }

        var currentValue = PocoPathHelper.GetValue(root, path, aotContexts);
        var incomingValue = PocoPathHelper.ConvertValue(setIntent.Value, property.PropertyType, aotContexts);

        if (!IsValidTransition(attribute.ValidatorType, property.PropertyType, currentValue, incomingValue))
        {
            throw new InvalidOperationException($"Invalid state transition from '{currentValue}' to '{incomingValue}'.");
        }

        return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, incomingValue, timestamp, clock);
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Upsert)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (property is null || !property.CanWrite)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }
        
        var attribute = property.StrategyAttribute as CrdtStateMachineStrategyAttribute;
        if (attribute is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var currentValue = PocoPathHelper.GetValue(root, operation.JsonPath, aotContexts);
        var incomingValue = PocoPathHelper.ConvertValue(operation.Value, property.PropertyType, aotContexts);

        if (!IsValidTransition(attribute.ValidatorType, property.PropertyType, currentValue, incomingValue))
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        if (metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs) && lwwTs.Timestamp is not null && operation.Timestamp.CompareTo(lwwTs.Timestamp) <= 0)
        {
            return CrdtOperationStatus.Obsolete;
        }
        
        PocoPathHelper.SetValue(root, operation.JsonPath, incomingValue, aotContexts);
        metadata.Lww[operation.JsonPath] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // StateMachineStrategy maintains a single active timestamp per property via LWW and does not maintain tombstones.
        // Therefore, there is no metadata to prune safely.
    }

    private bool IsValidTransition(Type validatorType, Type propertyType, object? from, object? to)
    {
        var validator = serviceProvider.GetService(validatorType);
        if (validator is null)
        {
            return false;
        }

        if (validator is IStateMachine stateMachine)
        {
            try
            {
                var fromState = from is null ? GetDefault(propertyType) : PocoPathHelper.ConvertValue(from, propertyType, aotContexts);
                var toState = to is null ? GetDefault(propertyType) : PocoPathHelper.ConvertValue(to, propertyType, aotContexts);
                
                return stateMachine.IsValidTransition(fromState, toState);
            }
            catch (Exception)
            {
                return false;
            }
        }

        return false;
    }

    private object? GetDefault(Type t)
    {
        return PocoPathHelper.GetDefaultValue(t, aotContexts);
    }
}