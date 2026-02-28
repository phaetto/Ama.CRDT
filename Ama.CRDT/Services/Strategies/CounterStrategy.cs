namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;
using System.Globalization;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;

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
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalNumeric = Convert.ToDecimal(originalValue ?? 0);
        var modifiedNumeric = Convert.ToDecimal(modifiedValue ?? 0);

        var delta = modifiedNumeric - originalNumeric;

        if (delta == 0)
        {
            return;
        }
        
        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, changeTimestamp);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (root, _, path, _, intent, timestamp, _) = context;

        return intent switch
        {
            IncrementIntent incrementIntent => new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                path,
                OperationType.Increment,
                Convert.ToDecimal(incrementIntent.Value ?? 0, CultureInfo.InvariantCulture),
                timestamp),

            SetIntent setIntent => GenerateSetOperation(root, path, setIntent, timestamp),

            _ => throw new NotSupportedException($"Explicit operation generation for intent '{intent.GetType().Name}' is not supported by {nameof(CounterStrategy)}.")
        };
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"{nameof(CounterStrategy)} only supports increment operations.");
        }

        var incrementValue = Convert.ToDecimal(operation.Value ?? 0, CultureInfo.InvariantCulture);
        var existingValue = PocoPathHelper.GetValue(root, operation.JsonPath) ?? 0;
        var existingNumeric = Convert.ToDecimal(existingValue, CultureInfo.InvariantCulture);
        var newValue = existingNumeric + incrementValue;
        
        PocoPathHelper.SetValue(root, operation.JsonPath, newValue);
    }

    private CrdtOperation GenerateSetOperation(object root, string path, SetIntent intent, ICrdtTimestamp timestamp)
    {
        var targetValue = Convert.ToDecimal(intent.Value ?? 0, CultureInfo.InvariantCulture);
        var existingValue = PocoPathHelper.GetValue(root, path) ?? 0;
        var currentValue = Convert.ToDecimal(existingValue, CultureInfo.InvariantCulture);
        var delta = targetValue - currentValue;

        return new CrdtOperation(
            Guid.NewGuid(),
            replicaId,
            path,
            OperationType.Increment,
            delta,
            timestamp);
    }
}