namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
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
public sealed class LwwStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var propertyType = property.PropertyType;
        if (propertyType.IsClass && propertyType != typeof(string) && !CrdtPatcher.IsCollection(propertyType))
        {
            var diffContext = new DifferentiateObjectContext(path, property.PropertyType, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, operations, changeTimestamp);
            patcher.DifferentiateObject(diffContext);
            return;
        }

        if (Equals(originalValue, modifiedValue))
        {
            return;
        }
        
        originalMeta.Lww.TryGetValue(path, out var originalTimestamp);
        
        if (originalTimestamp is not null && changeTimestamp.CompareTo(originalTimestamp) <= 0)
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp);
        
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
                context.ReplicaId,
                context.JsonPath,
                operationType,
                setIntent.Value,
                context.Timestamp);
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(LwwStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs);
        if (lwwTs is not null && operation.Timestamp.CompareTo(lwwTs) <= 0)
        {
            return;
        }
        
        if (operation.Type == OperationType.Remove)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, null);
            metadata.Lww[operation.JsonPath] = operation.Timestamp;
        }
        else if (operation.Type == OperationType.Upsert)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, operation.Value);
            metadata.Lww[operation.JsonPath] = operation.Timestamp;
        }
    }
}