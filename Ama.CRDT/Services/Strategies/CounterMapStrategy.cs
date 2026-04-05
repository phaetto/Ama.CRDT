namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;

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
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalDict = originalValue as IDictionary;
        var modifiedDict = modifiedValue as IDictionary;

        if (originalDict is null && modifiedDict is null) return;

        var typeInfo = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);

        var originalKeys = originalDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);
        var modifiedKeys = modifiedDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);

        var allKeys = originalKeys.Union(modifiedKeys, comparer);

        foreach (var key in allKeys)
        {
            var originalExists = originalKeys.Contains(key);
            var modifiedExists = modifiedKeys.Contains(key);

            var originalNumeric = originalExists ? PocoPathHelper.ConvertTo<decimal>(originalDict![key] ?? 0m, aotContexts) : 0m;
            var modifiedNumeric = modifiedExists ? PocoPathHelper.ConvertTo<decimal>(modifiedDict![key] ?? 0m, aotContexts) : 0m;

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
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Increment)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (parent is null || property is null || property.Getter!(parent) is not IDictionary dict)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var typeInfo = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        var valueType = typeInfo.DictionaryValueType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);
        
        IDictionary<object, PnCounterState> counters;
        if (metadata.States.TryGetValue(operation.JsonPath, out var baseState) && baseState is CounterMapState mapState)
        {
            counters = mapState.Keys;
        }
        else
        {
            counters = new Dictionary<object, PnCounterState>(comparer);
            metadata.States[operation.JsonPath] = new CounterMapState(counters);
        }

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>), aotContexts) is not KeyValuePair<object, object?> payload)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }
        
        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType, aotContexts);
        if (itemKey is null) return CrdtOperationStatus.StrategyApplicationFailed;

        if (!counters.TryGetValue(itemKey, out var counter))
        {
            counter = new PnCounterState(0, 0);
        }
        
        var delta = PocoPathHelper.ConvertTo<decimal>(payload.Value ?? 0m, aotContexts);

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
        dict[itemKey] = PocoPathHelper.ConvertValue(newValue, valueType, aotContexts);

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // CounterMapStrategy tracks independent PN-Counters per key, without tombstoning the keys themselves.
        // Therefore, there is no causal or time-based metadata (tombstones) to prune safely using ICompactionPolicy.
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        var dict = partitionableProperty.Getter!(data) as IDictionary;
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

        var payloadObj = PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>), aotContexts);
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
    public IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty)
    {
        var keyType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object);

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
            return (IComparable)PocoPathHelper.GetDefaultValue(keyType, aotContexts)!;
        }

        throw new InvalidOperationException($"Cannot determine minimum key for type {keyType}.");
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.States.TryGetValue(path, out var baseState) || baseState is not CounterMapState mapState || mapState.Keys.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var counters = mapState.Keys;
        var sortedKeys = counters.Keys.Cast<IComparable>().OrderBy(k => k).ToList();
        var splitIndex = sortedKeys.Count / 2;
        var splitKey = sortedKeys[splitIndex];

        var keys1 = sortedKeys.Take(splitIndex).ToHashSet();
        var keys2 = sortedKeys.Skip(splitIndex).ToHashSet();

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts)!;

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        var typeInfo = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        var valueType = typeInfo.DictionaryValueType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);

        var counters1 = new Dictionary<object, PnCounterState>(comparer);
        var counters2 = new Dictionary<object, PnCounterState>(comparer);

        if (meta1.States.TryGetValue(path, out var state1) && state1 is CounterMapState mapState1)
        {
            foreach (var kvp in mapState1.Keys)
            {
                if (keys1.Contains((IComparable)kvp.Key)) counters1[kvp.Key] = kvp.Value;
            }
        }
        meta1.States[path] = new CounterMapState(counters1);

        if (meta2.States.TryGetValue(path, out var state2) && state2 is CounterMapState mapState2)
        {
            foreach (var kvp in mapState2.Keys)
            {
                if (keys2.Contains((IComparable)kvp.Key)) counters2[kvp.Key] = kvp.Value;
            }
        }
        meta2.States[path] = new CounterMapState(counters2);

        ReconstructDictionaryForSplitMerge(doc1, path, counters1, keyType, valueType, aotContexts);
        ReconstructDictionaryForSplitMerge(doc2, path, counters2, keyType, valueType, aotContexts);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = data1.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var typeInfo = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        var valueType = typeInfo.DictionaryValueType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);

        var mergedCounters = new Dictionary<object, PnCounterState>(comparer);

        if (meta1.States.TryGetValue(path, out var state1) && state1 is CounterMapState mapState1)
        {
            foreach (var kvp in mapState1.Keys) mergedCounters[kvp.Key] = kvp.Value;
        }

        if (meta2.States.TryGetValue(path, out var state2) && state2 is CounterMapState mapState2)
        {
            foreach (var kvp in mapState2.Keys) mergedCounters[kvp.Key] = kvp.Value;
        }

        mergedMeta.States[path] = new CounterMapState(mergedCounters);

        ReconstructDictionaryForSplitMerge(mergedDoc, path, mergedCounters, keyType, valueType, aotContexts);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructDictionaryForSplitMerge(object root, string path, IDictionary<object, PnCounterState> counters, Type keyType, Type valueType, IEnumerable<CrdtAotContext> aotContexts)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path, aotContexts);
        if (parent is null || property is null) return;

        var dict = property.Getter!(parent) as IDictionary;
        
        if (dict is null)
        {
            dict = (IDictionary)PocoPathHelper.Instantiate(property.PropertyType, aotContexts)!;
            property.Setter!(parent, dict);
        }

        dict.Clear();
        foreach (var kvp in counters)
        {
            var newValue = kvp.Value.P - kvp.Value.N;
            var convertedKey = PocoPathHelper.ConvertValue(kvp.Key, keyType, aotContexts);
            
            if (convertedKey is not null)
            {
                dict[convertedKey] = PocoPathHelper.ConvertValue(newValue, valueType, aotContexts);
            }
        }
    }
}