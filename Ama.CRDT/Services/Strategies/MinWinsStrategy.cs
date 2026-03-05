namespace Ama.CRDT.Services.Strategies;

using System;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;

/// <summary>
/// Implements the Min-Wins Register strategy. Conflicts are resolved by choosing the lowest value.
/// </summary>
[CrdtSupportedType(typeof(IComparable))]
[CrdtSupportedIntent(typeof(SetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class MinWinsStrategy(ReplicaContext replicaContext) : ICrdtStrategy
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
        if (originalComparable.CompareTo(modifiedValue) > 0)
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
            if (setIntent.Value is not null && setIntent.Value is not IComparable)
            {
                throw new ArgumentException($"Value must implement {nameof(IComparable)} for {nameof(MinWinsStrategy)}.", nameof(context));
            }

            return new CrdtOperation(
                Id: Guid.NewGuid(),
                ReplicaId: replicaId,
                JsonPath: context.JsonPath,
                Type: OperationType.Upsert,
                Value: setIntent.Value,
                Timestamp: context.Timestamp,
                Clock: context.Clock
            );
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(MinWinsStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Upsert)
        {
            return;
        }

        var currentValue = PocoPathHelper.GetValue(root, operation.JsonPath);
        var incomingValue = operation.Value;

        if (incomingValue is null)
        {
            return;
        }

        if (currentValue is null || ((IComparable)currentValue).CompareTo(incomingValue) > 0)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, incomingValue);
        }
    }
}