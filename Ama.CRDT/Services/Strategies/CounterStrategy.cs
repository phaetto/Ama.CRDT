namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services.Providers;

/// <summary>
/// A CRDT strategy for handling numeric properties as counters.
/// It generates 'Increment' operations and applies them by adding the delta to the current value.
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
public sealed class CounterStrategy(ICrdtTimestampProvider timestampProvider, ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        var originalNumeric = Convert.ToDecimal(originalValue ?? 0);
        var modifiedNumeric = Convert.ToDecimal(modifiedValue ?? 0);

        var delta = modifiedNumeric - originalNumeric;

        if (delta == 0)
        {
            return;
        }
        
        modifiedMeta.Lww.TryGetValue(path, out var timestamp);
        if (timestamp is null)
        {
            timestamp = timestampProvider.Now();
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, timestamp);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"{nameof(CounterStrategy)} only supports increment operations.");
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);

        if (parent is null || property is null || !property.CanWrite) return;

        var incrementValue = Convert.ToDecimal(operation.Value ?? 0, CultureInfo.InvariantCulture);
        var existingValue = property.GetValue(parent) ?? 0;
        var existingNumeric = Convert.ToDecimal(existingValue, CultureInfo.InvariantCulture);
        var newValue = existingNumeric + incrementValue;
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var convertedNewValue = Convert.ChangeType(newValue, targetType, CultureInfo.InvariantCulture);

        property.SetValue(parent, convertedNewValue);
    }
}