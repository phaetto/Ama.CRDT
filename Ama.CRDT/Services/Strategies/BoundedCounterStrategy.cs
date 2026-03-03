namespace Ama.CRDT.Services.Strategies;

using System;
using System.Reflection;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services;

/// <summary>
/// Implements a Bounded Counter strategy, where the counter's value is clamped within a defined min/max range.
/// </summary>
[CrdtSupportedType(typeof(decimal))]
[CrdtSupportedType(typeof(double))]
[CrdtSupportedType(typeof(float))]
[CrdtSupportedType(typeof(int))]
[CrdtSupportedType(typeof(long))]
[CrdtSupportedIntent(typeof(IncrementIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class BoundedCounterStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

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
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp) = context;

        var originalNumeric = PocoPathHelper.ConvertTo<decimal>(originalValue);
        var modifiedNumeric = PocoPathHelper.ConvertTo<decimal>(modifiedValue);

        var delta = modifiedNumeric - originalNumeric;

        if (delta != 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, changeTimestamp);
            operations.Add(operation);
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is IncrementIntent incrementIntent)
        {
            return new CrdtOperation(
                Guid.NewGuid(),
                context.ReplicaId,
                context.JsonPath,
                OperationType.Increment,
                incrementIntent.Value,
                context.Timestamp);
        }

        throw new NotSupportedException($"Intent '{context.Intent.GetType().Name}' is not supported by {nameof(BoundedCounterStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"Bounded Counter strategy can only apply 'Increment' operations. Received '{operation.Type}'.");
        }

        var (_, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (property is null)
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
            unboundedValue = PocoPathHelper.GetValue<decimal>(root, operation.JsonPath);
        }

        var increment = PocoPathHelper.ConvertTo<decimal>(operation.Value);
        var newUnboundedValue = unboundedValue + increment;

        metadata.Lww[operation.JsonPath] = new UnboundedCounterValue(newUnboundedValue);

        var clampedValue = Math.Max(attribute.Min, Math.Min(attribute.Max, newUnboundedValue));

        PocoPathHelper.SetValue(root, operation.JsonPath, clampedValue);
    }
}