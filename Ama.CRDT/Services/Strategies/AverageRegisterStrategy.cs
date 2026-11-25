namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Collections;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Implements an Average Register strategy. Each replica contributes a value, and the property converges to the average of all contributions.
/// </summary>
[CrdtSupportedType(typeof(decimal))]
[CrdtSupportedType(typeof(double))]
[CrdtSupportedType(typeof(float))]
[CrdtSupportedType(typeof(int))]
[CrdtSupportedType(typeof(long))]
[CrdtSupportedIntent(typeof(SetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class AverageRegisterStrategy(
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
{
    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock, _) = context;
        if (Equals(originalValue, modifiedValue))
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), context.ReplicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp, clock);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is SetIntent setIntent)
        {
            return new CrdtOperation(
                Guid.NewGuid(),
                context.ReplicaId,
                context.JsonPath,
                OperationType.Upsert,
                setIntent.Value,
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"Intent '{context.Intent.GetType().Name}' is not supported by {nameof(AverageRegisterStrategy)}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation, _) = context;

        ArgumentNullException.ThrowIfNull(operation.ReplicaId);
        
        if (operation.Type != OperationType.Upsert)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        if (!metadata.States.TryGetValue(operation.JsonPath, out var state) || state is not AverageRegisterState avgState)
        {
            avgState = new AverageRegisterState(new CompactDictionary<string, AverageRegisterValue>());
            metadata.States[operation.JsonPath] = avgState;
        }
        var contributions = avgState.Contributions;
        
        if (contributions.TryGetValue(operation.ReplicaId, out var existing) && operation.Timestamp.CompareTo(existing.Timestamp) <= 0)
        {
            // Incoming operation is older or same, ignore.
            return CrdtOperationStatus.Obsolete;
        }
        
        var incomingValue = PocoPathHelper.ConvertTo<decimal>(operation.Value, aotContexts);
        contributions[operation.ReplicaId] = new AverageRegisterValue(incomingValue, operation.Timestamp);

        RecalculateAndApplyAverage(root, operation.JsonPath, contributions);

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // AverageRegisterStrategy does not maintain tombstones, only active replica contributions.
        // Therefore, there is no metadata to prune safely.
    }

    /// <inheritdoc/>
    public void MergeState(MergeStateContext context)
    {
        var (data1, meta1, data2, meta2, property, path) = context;
        
        if (!meta2.States.TryGetValue(path, out var state2) || state2 is not AverageRegisterState avgState2)
        {
            return;
        }

        if (!meta1.States.TryGetValue(path, out var state1) || state1 is not AverageRegisterState avgState1)
        {
            avgState1 = new AverageRegisterState(new CompactDictionary<string, AverageRegisterValue>(avgState2.Contributions.Count));
            meta1.States[path] = avgState1;
        }

        var contributions1 = avgState1.Contributions;
        var contributions2 = avgState2.Contributions;

        foreach (var kvp in contributions2)
        {
            if (!contributions1.TryGetValue(kvp.Key, out var existing) || kvp.Value.Timestamp.CompareTo(existing.Timestamp) > 0)
            {
                contributions1[kvp.Key] = kvp.Value;
            }
        }

        RecalculateAndApplyAverage(data1, path, contributions1);
    }

    private void RecalculateAndApplyAverage(object root, string jsonPath, IDictionary<string, AverageRegisterValue> contributions)
    {
        if (contributions.Count == 0)
        {
            return;
        }
        
        // Order by ReplicaId (Key) to ensure deterministic summation order across all replicas
        var sum = contributions.OrderBy(c => c.Key, StringComparer.Ordinal).Sum(c => c.Value.Value);
        var average = sum / contributions.Count;

        PocoPathHelper.SetValue(root, jsonPath, average, aotContexts);
    }
}