namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Implements the Last-Writer-Wins (LWW) strategy for conflict resolution. When a conflict occurs (i.e., multiple replicas modify the same property concurrently),
/// the value with the highest timestamp "wins" and is accepted as the final state. This strategy is suitable for simple properties, such as numbers, strings, or booleans.
/// For complex objects, this strategy recursively delegates the differentiation process.
/// </summary>
[CrdtSupportedType(typeof(object))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
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