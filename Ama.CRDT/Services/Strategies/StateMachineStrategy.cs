namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using System;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Services;

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
public sealed class StateMachineStrategy(ReplicaContext replicaContext, IServiceProvider serviceProvider) : ICrdtStrategy
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

        var attribute = property.GetCustomAttribute<CrdtStateMachineStrategyAttribute>();
        if (attribute is null) return;

        if (!IsValidTransition(attribute.ValidatorType, originalValue, modifiedValue))
        {
            return;
        }
        
        originalMeta.Lww.TryGetValue(path, out var originalTimestamp);
        
        if (originalTimestamp is not null && changeTimestamp.CompareTo(originalTimestamp) <= 0)
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

        var attribute = property.GetCustomAttribute<CrdtStateMachineStrategyAttribute>();
        if (attribute is null)
        {
            throw new InvalidOperationException($"Property {property.Name} is missing the {nameof(CrdtStateMachineStrategyAttribute)}.");
        }

        var currentValue = PocoPathHelper.GetValue(root, path);
        var incomingValue = PocoPathHelper.ConvertValue(setIntent.Value, property.PropertyType);

        if (!IsValidTransition(attribute.ValidatorType, currentValue, incomingValue))
        {
            throw new InvalidOperationException($"Invalid state transition from '{currentValue}' to '{incomingValue}'.");
        }

        return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, incomingValue, timestamp, clock);
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (_, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (property is null || !property.CanWrite) return;
        
        var attribute = property.GetCustomAttribute<CrdtStateMachineStrategyAttribute>();
        if (attribute is null) return;

        var currentValue = PocoPathHelper.GetValue(root, operation.JsonPath);
        var incomingValue = PocoPathHelper.ConvertValue(operation.Value, property.PropertyType);

        if (!IsValidTransition(attribute.ValidatorType, currentValue, incomingValue))
        {
            return;
        }

        metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs);
        if (lwwTs is not null && operation.Timestamp.CompareTo(lwwTs) <= 0)
        {
            return;
        }
        
        PocoPathHelper.SetValue(root, operation.JsonPath, incomingValue);
        metadata.Lww[operation.JsonPath] = operation.Timestamp;
    }

    private bool IsValidTransition(Type validatorType, object? from, object? to)
    {
        var validator = serviceProvider.GetService(validatorType);
        if (validator is null)
        {
            return false;
        }

        var stateMachineInterface = validatorType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStateMachine<>));
        
        if (stateMachineInterface is null) return false;

        var method = stateMachineInterface.GetMethod("IsValidTransition");
        if (method is null) return false;

        var stateType = stateMachineInterface.GetGenericArguments()[0];

        try
        {
            var fromState = from is null ? GetDefault(stateType) : PocoPathHelper.ConvertValue(from, stateType);
            var toState = to is null ? GetDefault(stateType) : PocoPathHelper.ConvertValue(to, stateType);
            
            return method.Invoke(validator, [fromState, toState]) is true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static object? GetDefault(Type t)
    {
        return t.IsValueType ? Activator.CreateInstance(t) : null;
    }
}