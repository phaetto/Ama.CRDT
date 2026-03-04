namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Services;

/// <summary>
/// Implements the Counter-Map strategy, where each key in a dictionary is an independent PN-Counter.
/// This strategy is partitionable, allowing large counter maps to be split and merged.
/// </summary>
[CrdtSupportedType(typeof(IDictionary))]
[CrdtSupportedIntent(typeof(MapIncrementIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class CounterMapStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

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

            var originalNumeric = originalExists ? PocoPathHelper.ConvertTo<decimal>(originalDict![key] ?? 0m) : 0m;
            var modifiedNumeric = modifiedExists ? PocoPathHelper.ConvertTo<decimal>(modifiedDict![key] ?? 0m) : 0m;

            var delta = modifiedNumeric - originalNumeric;

            if (delta != 0)
            {
                var payload = new KeyValuePair<object, object?>(key, delta);
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, payload, changeTimestamp, clock));
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is MapIncrementIntent mapIncrementIntent)
        {
            if (mapIncrementIntent.Key is null)
            {
                throw new ArgumentException("Key cannot be null for MapIncrementIntent.", nameof(context));
            }

            var payload = new KeyValuePair<object, object?>(mapIncrementIntent.Key, mapIncrementIntent.Value);
            return new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Increment,
                payload,
                context.Timestamp,
                context.Clock
            );
        }

        throw new NotSupportedException($"Intent {context.Intent?.GetType().Name} is not supported for {nameof(CounterMapStrategy)}.");
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
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IDictionary dict) return;

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
        
        var delta = PocoPathHelper.ConvertTo<decimal>(payload.Value ?? 0m);

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

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var dict = PocoPathHelper.GetAccessor(partitionableProperty).Getter(data) as IDictionary;
        if (dict == null || dict.Count == 0) return null;

        var sortedKeys = dict.Keys.Cast<IComparable>().OrderBy(k => k).ToList();
        return sortedKeys.FirstOrDefault();
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal))
        {
            return null;
        }

        var payloadObj = PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>));
        if (payloadObj is KeyValuePair<object, object?> payload)
        {
            if (payload.Key is IComparable comparableKey)
            {
                return comparableKey;
            }

            if (payload.Key?.ToString() is IComparable strKey)
            {
                return strKey;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);

        if (keyType == typeof(string)) return string.Empty;
        if (keyType == typeof(int)) return int.MinValue;
        if (keyType == typeof(long)) return long.MinValue;
        if (keyType == typeof(short)) return short.MinValue;
        if (keyType == typeof(byte)) return byte.MinValue;
        if (keyType == typeof(Guid)) return Guid.Empty;
        if (keyType == typeof(DateTime)) return DateTime.MinValue;
        if (keyType == typeof(DateTimeOffset)) return DateTimeOffset.MinValue;
        if (keyType == typeof(char)) return char.MinValue;
        if (keyType == typeof(double)) return double.MinValue;
        if (keyType == typeof(float)) return float.MinValue;
        if (keyType == typeof(decimal)) return decimal.MinValue;

        if (keyType.IsValueType)
        {
            return (IComparable)Activator.CreateInstance(keyType)!;
        }

        throw new InvalidOperationException($"Cannot determine minimum key for type {keyType}.");
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.CounterMaps.TryGetValue(path, out var counters) || counters.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedKeys = counters.Keys.Cast<IComparable>().OrderBy(k => k).ToList();
        var splitIndex = sortedKeys.Count / 2;
        var splitKey = sortedKeys[splitIndex];

        var keys1 = sortedKeys.Take(splitIndex).ToHashSet();
        var keys2 = sortedKeys.Skip(splitIndex).ToHashSet();

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);
        var valueType = PocoPathHelper.GetDictionaryValueType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(keyType);

        var counters1 = new Dictionary<object, PnCounterState>(comparer);
        var counters2 = new Dictionary<object, PnCounterState>(comparer);

        foreach (var kvp in meta1.CounterMaps[path])
        {
            if (keys1.Contains((IComparable)kvp.Key)) counters1[kvp.Key] = kvp.Value;
        }
        meta1.CounterMaps[path] = counters1;

        foreach (var kvp in meta2.CounterMaps[path])
        {
            if (keys2.Contains((IComparable)kvp.Key)) counters2[kvp.Key] = kvp.Value;
        }
        meta2.CounterMaps[path] = counters2;

        ReconstructDictionaryForSplitMerge(doc1, path, counters1, keyType, valueType);
        ReconstructDictionaryForSplitMerge(doc2, path, counters2, keyType, valueType);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);
        var valueType = PocoPathHelper.GetDictionaryValueType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(keyType);

        var mergedCounters = new Dictionary<object, PnCounterState>(comparer);

        if (meta1.CounterMaps.TryGetValue(path, out var counters1))
        {
            foreach (var kvp in counters1) mergedCounters[kvp.Key] = kvp.Value;
        }

        if (meta2.CounterMaps.TryGetValue(path, out var counters2))
        {
            foreach (var kvp in counters2) mergedCounters[kvp.Key] = kvp.Value;
        }

        mergedMeta.CounterMaps[path] = mergedCounters;

        ReconstructDictionaryForSplitMerge(mergedDoc, path, mergedCounters, keyType, valueType);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructDictionaryForSplitMerge(object root, string path, IDictionary<object, PnCounterState> counters, Type keyType, Type valueType)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        if (parent is null || property is null) return;

        var dict = PocoPathHelper.GetAccessor(property).Getter(parent) as IDictionary;
        
        if (dict is null)
        {
            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            dict = (IDictionary)Activator.CreateInstance(dictType)!;
            PocoPathHelper.GetAccessor(property).Setter(parent, dict);
        }

        dict.Clear();
        foreach (var kvp in counters)
        {
            var newValue = kvp.Value.P - kvp.Value.N;
            var convertedKey = PocoPathHelper.ConvertValue(kvp.Key, keyType);
            
            if (convertedKey is not null)
            {
                dict[convertedKey] = PocoPathHelper.ConvertValue(newValue, valueType);
            }
        }
    }
}