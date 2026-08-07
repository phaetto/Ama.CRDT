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
/// Implements the G-Counter (Grow-Only Counter) strategy. This counter only supports positive increments.
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
public sealed class GCounterStrategy(
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

        if (delta > 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, changeTimestamp, clock);
            operations.Add(operation);
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is not IncrementIntent incrementIntent)
        {
            throw new NotSupportedException($"Intent '{context.Intent.GetType().Name}' is not supported by {nameof(GCounterStrategy)}.");
        }

        var increment = PocoPathHelper.ConvertTo<decimal>(incrementIntent.Value, aotContexts);

        if (increment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "G-Counters only support positive increments.");
        }

        return new CrdtOperation(
            Guid.NewGuid(),
            replicaId,
            context.JsonPath,
            OperationType.Increment,
            increment,
            context.Timestamp,
            context.Clock);
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

        var increment = PocoPathHelper.ConvertTo<decimal>(operation.Value, aotContexts);
        if (increment <= 0)
        {
            // G-Counters only grow. Invalid non-positive increments are failed.
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var currentNumeric = PocoPathHelper.GetValue<decimal>(root, operation.JsonPath, aotContexts);
        var newValue = currentNumeric + increment;

        PocoPathHelper.SetValue(root, operation.JsonPath, newValue, aotContexts);

        if (!metadata.States.TryGetValue(operation.JsonPath, out var state) || state is not GCounterState gState)
        {
            gState = new GCounterState(new Dictionary<string, decimal>());
            metadata.States[operation.JsonPath] = gState;
        }

        var contributions = gState.Contributions;
        contributions.TryGetValue(operation.ReplicaId, out var existing);
        contributions[operation.ReplicaId] = existing + increment;

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // GCounterStrategy does not maintain tombstones.
    }

    /// <inheritdoc/>
    public void MergeAsStateCrdt(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo property)
    {
        var path = $"$.{char.ToLowerInvariant(property.Name[0])}{property.Name[1..]}";

        if (!meta1.States.TryGetValue(path, out var state1) || state1 is not GCounterState gState1)
        {
            gState1 = new GCounterState(new Dictionary<string, decimal>());
            meta1.States[path] = gState1;
        }

        if (!meta2.States.TryGetValue(path, out var state2) || state2 is not GCounterState gState2)
        {
            return;
        }

        decimal netDiff = 0m;

        foreach (var kvp in gState2.Contributions)
        {
            gState1.Contributions.TryGetValue(kvp.Key, out var existing1);
            if (kvp.Value > existing1)
            {
                netDiff += (kvp.Value - existing1);
                gState1.Contributions[kvp.Key] = kvp.Value;
            }
        }

        if (netDiff > 0m)
        {
            var val1 = PocoPathHelper.GetValue<decimal>(data1, path, aotContexts);
            PocoPathHelper.SetValue(data1, path, val1 + netDiff, aotContexts);
        }
    }
}