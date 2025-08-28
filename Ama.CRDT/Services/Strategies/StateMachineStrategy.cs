namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Services;

/// <summary>
/// Implements a State Machine strategy. This strategy enforces valid state transitions
/// while using a Last-Writer-Wins (LWW) mechanism for conflict resolution among valid transitions.
/// </summary>
[CrdtSupportedType(typeof(object))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class StateMachineStrategy(ReplicaContext replicaContext, IServiceProvider serviceProvider) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
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
        
        modifiedMeta.Lww.TryGetValue(path, out var modifiedTimestamp);
        originalMeta.Lww.TryGetValue(path, out var originalTimestamp);
        
        if (modifiedTimestamp is null || originalTimestamp is not null && modifiedTimestamp.CompareTo(originalTimestamp) <= 0)
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, modifiedTimestamp);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || !property.CanWrite) return;
        
        var attribute = property.GetCustomAttribute<CrdtStateMachineStrategyAttribute>();
        if (attribute is null) return;

        var currentValue = property.GetValue(parent);
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

        property.SetValue(parent, incomingValue);
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
            var fromState = from is null ? GetDefault(stateType) : Convert.ChangeType(from, stateType);
            var toState = to is null ? GetDefault(stateType) : Convert.ChangeType(to, stateType);
            
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