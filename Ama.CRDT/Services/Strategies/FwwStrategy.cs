namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;

/// <summary>
/// Implements the First-Writer-Wins (FWW) strategy for conflict resolution. When a conflict occurs (i.e., multiple replicas modify the same property concurrently),
/// the value with the lowest timestamp "wins" and is accepted as the final state. This strategy is suitable for properties that should only be set once.
/// </summary>
[CrdtSupportedType(typeof(object))]
[CrdtSupportedIntent(typeof(SetIntent))]
[CrdtSupportedIntent(typeof(ClearIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class FwwStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        if (Equals(originalValue, modifiedValue))
        {
            return;
        }
        
        originalMeta.Fww.TryGetValue(path, out var originalTimestamp);
        
        if (originalTimestamp is not null && changeTimestamp.CompareTo(originalTimestamp) >= 0)
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp, clock);
        
        if (modifiedValue is null)
        {
            operation = operation with { Type = OperationType.Remove, Value = null };
        }
        
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is SetIntent setIntent)
        {
            var operationType = setIntent.Value is null ? OperationType.Remove : OperationType.Upsert;
            return new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                operationType,
                setIntent.Value,
                context.Timestamp,
                context.Clock);
        }

        if (context.Intent is ClearIntent)
        {
            return new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Remove,
                null,
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(FwwStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        bool isReset = operation.Type == OperationType.Remove && operation.Value is null;

        metadata.Fww.TryGetValue(operation.JsonPath, out var fwwTs);
        if (!isReset && fwwTs is not null && operation.Timestamp.CompareTo(fwwTs) >= 0)
        {
            return;
        }
        
        if (isReset)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, null);
            metadata.Fww.Remove(operation.JsonPath);
        }
        else if (operation.Type == OperationType.Remove)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, null);
            metadata.Fww[operation.JsonPath] = operation.Timestamp;
        }
        else if (operation.Type == OperationType.Upsert)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, operation.Value);
            metadata.Fww[operation.JsonPath] = operation.Timestamp;
        }
    }
}