namespace Ama.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;

/// <summary>
/// Implements the Max-Wins Register strategy. Conflicts are resolved by choosing the highest value.
/// </summary>
[Commutative]
[Associative]
[Idempotent]
public sealed class MaxWinsStrategy(IOptions<CrdtOptions> options, ICrdtTimestampProvider timestampProvider) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        if (modifiedValue is null || originalValue is null)
        {
            if (modifiedValue != originalValue)
            {
                var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, timestampProvider.Now());
                operations.Add(operation);
            }
            return;
        }
        
        var originalComparable = (IComparable)originalValue;
        if (originalComparable.CompareTo(modifiedValue) < 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, timestampProvider.Now());
            operations.Add(operation);
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        if (operation.Type != OperationType.Upsert)
        {
            // This strategy only handles value assignments.
            return;
        }
        
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null)
        {
            return;
        }

        var currentValue = property.GetValue(parent);
        var incomingValue = operation.Value;

        if (incomingValue is null)
        {
            return; // Max-wins doesn't typically handle nulls; we ignore them.
        }

        if (currentValue is null || ((IComparable)currentValue).CompareTo(incomingValue) < 0)
        {
            var convertedValue = PocoPathHelper.ConvertValue(incomingValue, property.PropertyType);
            property.SetValue(parent, convertedValue);
        }
    }
}