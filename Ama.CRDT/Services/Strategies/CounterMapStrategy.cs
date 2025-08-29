namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ama.CRDT.Services;

/// <summary>
/// Implements the Counter-Map strategy, where each key in a dictionary is an independent PN-Counter.
/// </summary>
[CrdtSupportedType(typeof(IDictionary))]
[Commutative]
[Associative]
[IdempotentWithContinuousTime]
[Mergeable]
public sealed class CounterMapStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalDict = originalValue as IDictionary;
        var modifiedDict = modifiedValue as IDictionary;

        if (originalDict is null && modifiedDict is null) return;

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var comparer = comparerProvider.GetComparer(keyType);

        var originalKeys = originalDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);
        var modifiedKeys = modifiedDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);

        var allKeys = originalKeys.Union(modifiedKeys, comparer);

        foreach (var key in allKeys)
        {
            var originalExists = originalKeys.Contains(key);
            var modifiedExists = modifiedKeys.Contains(key);

            var originalNumeric = originalExists ? Convert.ToDecimal(originalDict![key] ?? 0) : 0;
            var modifiedNumeric = modifiedExists ? Convert.ToDecimal(modifiedDict![key] ?? 0) : 0;

            var delta = modifiedNumeric - originalNumeric;

            if (delta != 0)
            {
                var payload = new KeyValuePair<object, object?>(key, delta);
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, payload, changeTimestamp));
            }
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"{nameof(CounterMapStrategy)} only supports increment operations.");
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IDictionary dict) return;

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);
        var comparer = comparerProvider.GetComparer(keyType);
        
        if (!metadata.CounterMaps.TryGetValue(operation.JsonPath, out var counters))
        {
            counters = new Dictionary<object, PnCounterState>(comparer);
            metadata.CounterMaps[operation.JsonPath] = counters;
        }

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>)) is not KeyValuePair<object, object?> payload)
        {
            return;
        }
        
        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null) return;

        if (!counters.TryGetValue(itemKey, out var counter))
        {
            counter = new PnCounterState(0, 0);
        }
        
        var delta = Convert.ToDecimal(payload.Value ?? 0, CultureInfo.InvariantCulture);

        if (delta > 0)
        {
            counter = counter with { P = counter.P + delta };
        }
        else
        {
            counter = counter with { N = counter.N - delta };
        }

        counters[itemKey] = counter;
        
        var newValue = counter.P - counter.N;
        dict[itemKey] = PocoPathHelper.ConvertValue(newValue, valueType);
    }
}