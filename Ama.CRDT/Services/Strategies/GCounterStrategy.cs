namespace Ama.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services;

/// <summary>
/// Implements the G-Counter (Grow-Only Counter) strategy. This counter only supports positive increments.
/// </summary>
[CrdtSupportedType(typeof(decimal))]
[CrdtSupportedType(typeof(double))]
[CrdtSupportedType(typeof(float))]
[CrdtSupportedType(typeof(int))]
[CrdtSupportedType(typeof(long))]
[Commutative]
[Associative]
[IdempotentWithContinuousTime]
[Mergeable]
public sealed class GCounterStrategy(ICrdtTimestampProvider timestampProvider, ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        var originalNumeric = originalValue is not null ? Convert.ToDecimal(originalValue) : 0;
        var modifiedNumeric = modifiedValue is not null ? Convert.ToDecimal(modifiedValue) : 0;

        var delta = modifiedNumeric - originalNumeric;

        if (delta > 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, timestampProvider.Now());
            operations.Add(operation);
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"G-Counter strategy can only apply 'Increment' operations. Received '{operation.Type}'.");
        }

        var increment = operation.Value is not null ? Convert.ToDecimal(operation.Value) : 0;
        if (increment <= 0)
        {
            // G-Counters only grow. Silently ignore non-positive increments.
            return;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null)
        {
            return;
        }

        var currentValue = property.GetValue(parent);
        var currentNumeric = currentValue is not null ? Convert.ToDecimal(currentValue) : 0;

        var newValue = currentNumeric + increment;
        var convertedValue = Convert.ChangeType(newValue, property.PropertyType);

        property.SetValue(parent, convertedValue);
    }
}