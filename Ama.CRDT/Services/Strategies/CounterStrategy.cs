namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;
using System.Globalization;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;

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
}