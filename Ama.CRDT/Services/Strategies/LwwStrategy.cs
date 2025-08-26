namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.Options;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Implements the Last-Writer-Wins (LWW) strategy.
/// </summary>
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class LwwStrategy : ICrdtStrategy
{
    private readonly string replicaId;

    /// <summary>
    /// Initializes a new instance of the <see cref="LwwStrategy"/> class.
    /// </summary>
    /// <param name="options">Configuration options containing the replica ID.</param>
    public LwwStrategy(IOptions<CrdtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        replicaId = options.Value.ReplicaId;
    }

    /// <inheritdoc/>
    public void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta)
    {
        var propertyType = property.PropertyType;
        if (propertyType.IsClass && propertyType != typeof(string) && !CrdtPatcher.IsCollection(propertyType))
        {
            patcher.DifferentiateObject(path, property.PropertyType, originalValue, originalMeta, modifiedValue, modifiedMeta, operations, originalRoot, modifiedRoot);
            return;
        }

        if (Equals(originalValue, modifiedValue))
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
        
        if (modifiedValue is null)
        {
            operation = operation with { Type = OperationType.Remove, Value = null };
        }
        
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs);
        if (lwwTs is not null && operation.Timestamp.CompareTo(lwwTs) <= 0)
        {
            return;
        }

        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(root, operation.JsonPath);

        if (parent is null) return;

        if (property is null)
        {
            if (finalSegment is string propertyName)
            {
                property = parent.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            }
        }

        if (property is null || !property.CanWrite) return;
        
        if (operation.Type == OperationType.Remove)
        {
            property.SetValue(parent, null);
            metadata.Lww[operation.JsonPath] = operation.Timestamp;
        }
        else if (operation.Type == OperationType.Upsert)
        {
            var value = PocoPathHelper.ConvertValue(operation.Value, property.PropertyType);
            property.SetValue(parent, value);
            metadata.Lww[operation.JsonPath] = operation.Timestamp;
        }
    }
}