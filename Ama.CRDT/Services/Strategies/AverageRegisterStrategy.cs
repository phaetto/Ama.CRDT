namespace Ama.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;

/// <summary>
/// Implements an Average Register strategy. Each replica contributes a value, and the property converges to the average of all contributions.
/// </summary>
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class AverageRegisterStrategy(IOptions<CrdtOptions> options, ICrdtTimestampProvider timestampProvider) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        if (Equals(originalValue, modifiedValue))
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, timestampProvider.Now());
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation.ReplicaId);
        
        if (operation.Type != OperationType.Upsert)
        {
            return;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null)
        {
            return;
        }

        if (!metadata.AverageRegisters.TryGetValue(operation.JsonPath, out var contributions))
        {
            contributions = new Dictionary<string, AverageRegisterValue>();
            metadata.AverageRegisters[operation.JsonPath] = contributions;
        }
        
        if (contributions.TryGetValue(operation.ReplicaId, out var existing) && operation.Timestamp.CompareTo(existing.Timestamp) <= 0)
        {
            // Incoming operation is older or same, ignore.
            return;
        }
        
        var incomingValue = operation.Value is not null ? Convert.ToDecimal(operation.Value) : 0;
        contributions[operation.ReplicaId] = new AverageRegisterValue(incomingValue, operation.Timestamp);

        RecalculateAndApplyAverage(parent, property, contributions);
    }

    private static void RecalculateAndApplyAverage(object parent, PropertyInfo property, IDictionary<string, AverageRegisterValue> contributions)
    {
        if (contributions.Count == 0)
        {
            return;
        }
        
        var sum = contributions.Values.Sum(c => c.Value);
        var average = sum / contributions.Count;

        var convertedValue = Convert.ChangeType(average, property.PropertyType);
        property.SetValue(parent, convertedValue);
    }
}