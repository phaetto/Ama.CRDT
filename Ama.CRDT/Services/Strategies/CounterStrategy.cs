namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;

/// <summary>
/// A CRDT strategy for handling numeric properties as counters.
/// It generates 'Increment' operations and applies them by adding the delta to the current value.
/// Supports both explicit Increment and Set intents.
/// </summary>
[CrdtSupportedType(typeof(decimal))]
[CrdtSupportedType(typeof(double))]
[CrdtSupportedType(typeof(float))]
[CrdtSupportedType(typeof(int))]
[CrdtSupportedType(typeof(long))]
[CrdtSupportedIntent(typeof(IncrementIntent))]
[CrdtSupportedIntent(typeof(SetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class CounterStrategy(
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalNumeric = PocoPathHelper.ConvertTo<decimal>(originalValue, aotContexts);
        var modifiedNumeric = PocoPathHelper.ConvertTo<decimal>(modifiedValue, aotContexts);

        var delta = modifiedNumeric - originalNumeric;

        if (delta == 0m)
        {
            return;
        }
        
        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, changeTimestamp, clock);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (root, _, path, _, intent, timestamp, clock) = context;

        return intent switch
        {
            IncrementIntent incrementIntent => new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                path,
                OperationType.Increment,
                PocoPathHelper.ConvertTo<decimal>(incrementIntent.Value, aotContexts),
                timestamp,
                clock),

            SetIntent setIntent => GenerateSetOperation(root, path, setIntent, timestamp, clock),

            _ => throw new NotSupportedException($"Explicit operation generation for intent '{intent.GetType().Name}' is not supported by {nameof(CounterStrategy)}.")
        };
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

        var incrementValue = PocoPathHelper.ConvertTo<decimal>(operation.Value, aotContexts);
        var existingValue = PocoPathHelper.GetValue<decimal>(root, operation.JsonPath, aotContexts);
        var newValue = existingValue + incrementValue;
        
        PocoPathHelper.SetValue(root, operation.JsonPath, newValue, aotContexts);

        if (!metadata.States.TryGetValue(operation.JsonPath, out var state) || state is not CounterState cState)
        {
            cState = new CounterState(new Dictionary<string, PnCounterState>());
            metadata.States[operation.JsonPath] = cState;
        }

        var contributions = cState.Contributions;
        if (!contributions.TryGetValue(operation.ReplicaId, out var counter))
        {
            counter = new PnCounterState(0m, 0m);
        }

        if (incrementValue > 0)
        {
            counter = counter with { P = counter.P + incrementValue };
        }
        else
        {
            counter = counter with { N = counter.N - incrementValue }; // Subtracting a negative creates a positive tracking delta
        }

        contributions[operation.ReplicaId] = counter;

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // CounterStrategy does not maintain tombstones.
        // Therefore, there is no metadata to prune safely.
    }

    /// <inheritdoc/>
    public void MergeState(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo property)
    {
        var path = $"$.{char.ToLowerInvariant(property.Name[0])}{property.Name[1..]}";

        if (!meta1.States.TryGetValue(path, out var state1) || state1 is not CounterState cState1)
        {
            cState1 = new CounterState(new Dictionary<string, PnCounterState>());
            meta1.States[path] = cState1;
        }

        if (!meta2.States.TryGetValue(path, out var state2) || state2 is not CounterState cState2)
        {
            return;
        }

        decimal netDiff = 0m;

        foreach (var kvp in cState2.Contributions)
        {
            cState1.Contributions.TryGetValue(kvp.Key, out var existing1);
            var existingP = existing1?.P ?? 0m;
            var existingN = existing1?.N ?? 0m;

            var diffP = Math.Max(0m, kvp.Value.P - existingP);
            var diffN = Math.Max(0m, kvp.Value.N - existingN);

            if (diffP > 0m || diffN > 0m)
            {
                netDiff += diffP;
                netDiff -= diffN;
                cState1.Contributions[kvp.Key] = new PnCounterState(
                    Math.Max(existingP, kvp.Value.P), 
                    Math.Max(existingN, kvp.Value.N));
            }
        }

        if (netDiff != 0m)
        {
            var val1 = PocoPathHelper.GetValue<decimal>(data1, path, aotContexts);
            PocoPathHelper.SetValue(data1, path, val1 + netDiff, aotContexts);
        }
    }

    private CrdtOperation GenerateSetOperation(object root, string path, SetIntent intent, ICrdtTimestamp timestamp, long clock)
    {
        var targetValue = PocoPathHelper.ConvertTo<decimal>(intent.Value, aotContexts);
        var currentValue = PocoPathHelper.GetValue<decimal>(root, path, aotContexts);
        var delta = targetValue - currentValue;

        return new CrdtOperation(
            Guid.NewGuid(),
            replicaId,
            path,
            OperationType.Increment,
            delta,
            timestamp,
            clock);
    }
}