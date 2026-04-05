namespace Ama.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;

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
public sealed class BoundedCounterStrategy(ReplicaContext replicaContext, IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
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
        var (operations, _, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalNumeric = PocoPathHelper.ConvertTo<decimal>(originalValue, aotContexts);
        var modifiedNumeric = PocoPathHelper.ConvertTo<decimal>(modifiedValue, aotContexts);

        var delta = modifiedNumeric - originalNumeric;

        if (delta != 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, changeTimestamp, clock);
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
                replicaId,
                context.JsonPath,
                OperationType.Increment,
                incrementIntent.Value,
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"Intent '{context.Intent.GetType().Name}' is not supported by {nameof(BoundedCounterStrategy)}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Increment)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var property = context.Property;
        if (property is null)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var attribute = property.StrategyAttribute as CrdtBoundedCounterStrategyAttribute;
        if (attribute is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        decimal unboundedValue;
        if (metadata.States.TryGetValue(operation.JsonPath, out var baseState) && baseState is CausalTimestamp timestamp && timestamp.Timestamp is UnboundedCounterValue counterValue)
        {
            unboundedValue = counterValue.Value;
        }
        else
        {
            unboundedValue = PocoPathHelper.GetValue<decimal>(root, operation.JsonPath, aotContexts);
        }

        var increment = PocoPathHelper.ConvertTo<decimal>(operation.Value, aotContexts);
        var newUnboundedValue = unboundedValue + increment;

        metadata.States[operation.JsonPath] = new CausalTimestamp(new UnboundedCounterValue(newUnboundedValue), operation.ReplicaId, operation.Clock);

        var clampedValue = Math.Max(attribute.Min, Math.Min(attribute.Max, newUnboundedValue));

        PocoPathHelper.SetValue(root, operation.JsonPath, clampedValue, aotContexts);

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // BoundedCounterStrategy does not maintain tombstones, only the current unbounded value.
        // Therefore, there is no metadata to prune safely using the ICompactionPolicy.
    }
}