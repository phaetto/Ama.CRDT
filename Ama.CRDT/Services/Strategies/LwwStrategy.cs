namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;

/// <summary>
/// Implements the Last-Writer-Wins (LWW) strategy for conflict resolution. When a conflict occurs (i.e., multiple replicas modify the same property concurrently),
/// the value with the highest timestamp "wins" and is accepted as the final state. This strategy is suitable for simple properties, such as numbers, strings, or booleans.
/// For complex objects, this strategy recursively delegates the differentiation process.
/// </summary>
[CrdtSupportedType(typeof(object))]
[CrdtSupportedIntent(typeof(SetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class LwwStrategy(
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
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
        
        if (originalMeta.States.TryGetValue(path, out var baseState) && baseState is CausalTimestamp originalTimestamp && originalTimestamp.Timestamp is not null && changeTimestamp.CompareTo(originalTimestamp.Timestamp) <= 0)
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

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(LwwStrategy)}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (metadata.States.TryGetValue(operation.JsonPath, out var baseState) && baseState is CausalTimestamp lwwTs && lwwTs.Timestamp is not null && operation.Timestamp.CompareTo(lwwTs.Timestamp) <= 0)
        {
            return CrdtOperationStatus.Obsolete;
        }
        
        if (operation.Type == OperationType.Remove)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, null, aotContexts);
            metadata.States[operation.JsonPath] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
        }
        else if (operation.Type == OperationType.Upsert)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, operation.Value, aotContexts);
            metadata.States[operation.JsonPath] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
        }
        else
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // LwwStrategy maintains a single active timestamp per property and does not maintain tombstones.
        // Therefore, there is no metadata to prune safely.
    }
}