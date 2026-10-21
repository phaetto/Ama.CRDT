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

        ArgumentNullException.ThrowIfNull(operation.ReplicaId);

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

        var increment = PocoPathHelper.ConvertTo<decimal>(operation.Value, aotContexts);

        if (!metadata.States.TryGetValue(operation.JsonPath, out var baseState) || baseState is not CounterState cState)
        {
            var initialValue = PocoPathHelper.GetValue<decimal>(root, operation.JsonPath, aotContexts);
            var contributions = new Dictionary<string, PnCounterState>();
            if (initialValue != 0m)
            {
                contributions[string.Empty] = new PnCounterState(
                    initialValue > 0m ? initialValue : 0m,
                    initialValue < 0m ? -initialValue : 0m);
            }

            cState = new CounterState(contributions);
            metadata.States[operation.JsonPath] = cState;
        }

        if (!cState.Contributions.TryGetValue(operation.ReplicaId, out var counter))
        {
            counter = new PnCounterState(0m, 0m);
        }

        if (increment > 0m)
        {
            counter = counter with { P = counter.P + increment };
        }
        else
        {
            counter = counter with { N = counter.N - increment };
        }

        cState.Contributions[operation.ReplicaId] = counter;

        var newUnboundedValue = CalculateUnboundedValue(cState);
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

    /// <inheritdoc/>
    public void MergeState(MergeStateContext context)
    {
        var (data1, meta1, data2, meta2, property, path) = context;

        if (!meta2.States.TryGetValue(path, out var state2) || state2 is not CounterState cState2)
        {
            return;
        }

        if (!meta1.States.TryGetValue(path, out var state1) || state1 is not CounterState cState1)
        {
            var initialValue = PocoPathHelper.GetValue<decimal>(data1, path, aotContexts);
            var contributions = new Dictionary<string, PnCounterState>();
            if (initialValue != 0m)
            {
                contributions[string.Empty] = new PnCounterState(
                    initialValue > 0m ? initialValue : 0m,
                    initialValue < 0m ? -initialValue : 0m);
            }

            cState1 = new CounterState(contributions);
            meta1.States[path] = cState1;
        }

        var updated = false;

        foreach (var kvp in cState2.Contributions)
        {
            var hasExisting = cState1.Contributions.TryGetValue(kvp.Key, out var existing1);
            var existingP = hasExisting ? existing1.P : 0m;
            var existingN = hasExisting ? existing1.N : 0m;

            var maxP = Math.Max(existingP, kvp.Value.P);
            var maxN = Math.Max(existingN, kvp.Value.N);

            if (maxP > existingP || maxN > existingN || !hasExisting)
            {
                cState1.Contributions[kvp.Key] = new PnCounterState(maxP, maxN);
                updated = true;
            }
        }

        if (updated)
        {
            var attribute = property.StrategyAttribute as CrdtBoundedCounterStrategyAttribute;
            if (attribute is not null)
            {
                var unboundedValue = CalculateUnboundedValue(cState1);
                var clampedValue = Math.Max(attribute.Min, Math.Min(attribute.Max, unboundedValue));
                PocoPathHelper.SetValue(data1, path, clampedValue, aotContexts);
            }
        }
    }

    private static decimal CalculateUnboundedValue(CounterState state)
    {
        var sum = 0m;
        foreach (var kvp in state.Contributions)
        {
            sum += kvp.Value.P - kvp.Value.N;
        }

        return sum;
    }
}