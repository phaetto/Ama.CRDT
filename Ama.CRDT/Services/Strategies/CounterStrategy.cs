namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Helpers;
using System;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;

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
public sealed class CounterStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalNumeric = PocoPathHelper.ConvertTo<decimal>(originalValue);
        var modifiedNumeric = PocoPathHelper.ConvertTo<decimal>(modifiedValue);

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
                PocoPathHelper.ConvertTo<decimal>(incrementIntent.Value),
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

        if (operation.Type != OperationType.Increment)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var incrementValue = PocoPathHelper.ConvertTo<decimal>(operation.Value);
        var existingValue = PocoPathHelper.GetValue<decimal>(root, operation.JsonPath);
        var newValue = existingValue + incrementValue;
        
        PocoPathHelper.SetValue(root, operation.JsonPath, newValue);

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // CounterStrategy does not maintain tombstones, only the numerical value directly on the document.
        // Therefore, there is no metadata to prune safely.
    }

    private CrdtOperation GenerateSetOperation(object root, string path, SetIntent intent, ICrdtTimestamp timestamp, long clock)
    {
        var targetValue = PocoPathHelper.ConvertTo<decimal>(intent.Value);
        var currentValue = PocoPathHelper.GetValue<decimal>(root, path);
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