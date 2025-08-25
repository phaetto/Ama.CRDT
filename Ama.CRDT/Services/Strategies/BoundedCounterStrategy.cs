namespace Ama.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;

/// <summary>
/// Implements a Bounded Counter strategy, where the counter's value is clamped within a defined min/max range.
/// </summary>
[Commutative]
[Associative]
[IdempotentShortTermImplementation]
[Mergeable]
public sealed class BoundedCounterStrategy(ICrdtTimestampProvider timestampProvider, IOptions<CrdtOptions> options) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;

    private readonly record struct UnboundedCounterValue(decimal Value) : ICrdtTimestamp
    {
        public int CompareTo(ICrdtTimestamp? other)
        {
            if (other is UnboundedCounterValue otherCounter)
            {
                return Value.CompareTo(otherCounter.Value);
            }
            // Not comparable with other timestamp types.
            return 0;
        }
    }

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        var originalNumeric = originalValue is not null ? Convert.ToDecimal(originalValue) : 0;
        var modifiedNumeric = modifiedValue is not null ? Convert.ToDecimal(modifiedValue) : 0;

        var delta = modifiedNumeric - originalNumeric;

        if (delta != 0)
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
            throw new InvalidOperationException($"Bounded Counter strategy can only apply 'Increment' operations. Received '{operation.Type}'.");
        }

        if (metadata.SeenExceptions.Contains(operation))
        {
            return;
        }

        try
        {
            var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
            if (parent is null || property is null)
            {
                return;
            }

            var attribute = property.GetCustomAttribute<CrdtBoundedCounterStrategyAttribute>();
            if (attribute is null)
            {
                throw new InvalidOperationException($"Property at path '{operation.JsonPath}' is missing the CrdtBoundedCounterStrategyAttribute.");
            }

            decimal unboundedValue;
            if (metadata.Lww.TryGetValue(operation.JsonPath, out var timestamp) && timestamp is UnboundedCounterValue counterValue)
            {
                unboundedValue = counterValue.Value;
            }
            else
            {
                var currentValue = property.GetValue(parent);
                unboundedValue = currentValue is not null ? Convert.ToDecimal(currentValue) : 0;
            }

            var increment = operation.Value is not null ? Convert.ToDecimal(operation.Value) : 0;
            var newUnboundedValue = unboundedValue + increment;

            metadata.Lww[operation.JsonPath] = new UnboundedCounterValue(newUnboundedValue);

            var clampedValue = Math.Max(attribute.Min, Math.Min(attribute.Max, newUnboundedValue));

            var convertedValue = Convert.ChangeType(clampedValue, property.PropertyType);
            property.SetValue(parent, convertedValue);
        }
        finally
        {
            metadata.SeenExceptions.Add(operation);
        }
    }
}