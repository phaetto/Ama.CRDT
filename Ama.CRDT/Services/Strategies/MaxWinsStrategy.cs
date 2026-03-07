namespace Ama.CRDT.Services.Strategies;

using System;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;

/// <summary>
/// Implements the Max-Wins Register strategy. Conflicts are resolved by choosing the highest value.
/// </summary>
[CrdtSupportedType(typeof(IComparable))]
[CrdtSupportedIntent(typeof(SetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class MaxWinsStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        if (modifiedValue is null || originalValue is null)
        {
            if (modifiedValue != originalValue)
            {
                var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp, clock);
                operations.Add(operation);
            }
            return;
        }
        
        var originalComparable = (IComparable)originalValue;
        if (originalComparable.CompareTo(modifiedValue) < 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp, clock);
            operations.Add(operation);
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is SetIntent setIntent)
        {
            return new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Upsert,
                setIntent.Value,
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"Explicit operation generation for intent '{context.Intent.GetType().Name}' is not supported for {this.GetType().Name}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Upsert)
        {
            // This strategy only handles value assignments.
            return CrdtOperationStatus.StrategyApplicationFailed;
        }
        
        var currentValue = PocoPathHelper.GetValue(root, operation.JsonPath);
        var incomingValue = operation.Value;

        if (incomingValue is null)
        {
            // Max-wins doesn't typically handle nulls; we ignore them but it is not a failure of the strategy.
            return CrdtOperationStatus.Success; 
        }

        if (currentValue is null || ((IComparable)currentValue).CompareTo(incomingValue) < 0)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, incomingValue);
        }

        return CrdtOperationStatus.Success;
    }
}