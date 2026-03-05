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
using System.Linq;
using System.Reflection;
using Ama.CRDT.Services;

/// <summary>
/// Implements the FWW-Map (First-Writer-Wins Map) CRDT strategy.
/// Each key-value pair is treated as an independent FWW-Register, meaning the lowest timestamp wins.
/// Supports partitioning allowing the dictionary to scale larger than memory.
/// </summary>
[CrdtSupportedType(typeof(IDictionary))]
[CrdtSupportedIntent(typeof(MapSetIntent))]
[CrdtSupportedIntent(typeof(MapRemoveIntent))]
[CrdtSupportedIntent(typeof(ClearIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class FwwMapStrategy(
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
        
        var addedKeys = modifiedKeys.Except(originalKeys, comparer);
        var removedKeys = originalKeys.Except(modifiedKeys, comparer);
        var commonKeys = originalKeys.Intersect(modifiedKeys, comparer);

        foreach (var key in addedKeys)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new KeyValuePair<object, object?>(key, modifiedDict![key]), changeTimestamp, clock));
        }

        foreach (var key in removedKeys)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new KeyValuePair<object, object?>(key, null), changeTimestamp, clock));
        }

        foreach (var key in commonKeys)
        {
            var originalItemValue = originalDict![key];
            var modifiedItemValue = modifiedDict![key];

            if (!Equals(originalItemValue, modifiedItemValue))
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new KeyValuePair<object, object?>(key, modifiedItemValue), changeTimestamp, clock));
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (_, _, path, _, intent, timestamp, clock) = context;

        return intent switch
        {
            MapSetIntent mapSet => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new KeyValuePair<object, object?>(mapSet.Key, mapSet.Value), timestamp, clock),
            MapRemoveIntent mapRemove => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new KeyValuePair<object, object?>(mapRemove.Key, null), timestamp, clock),
            ClearIntent => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, null, timestamp, clock),
            _ => throw new NotSupportedException($"Explicit operation generation for intent '{intent.GetType().Name}' is not supported for {this.GetType().Name}.")
        };
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IDictionary dict) return;

        bool isReset = operation.Type == OperationType.Remove && operation.Value is null;
        if (isReset)
        {
            dict.Clear();
            metadata.FwwMaps.Remove(operation.JsonPath);
            return;
        }

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);
        var comparer = comparerProvider.GetComparer(keyType);

        if (!metadata.FwwMaps.TryGetValue(operation.JsonPath, out var timestamps))
        {
            timestamps = new Dictionary<object, ICrdtTimestamp>(comparer);
            metadata.FwwMaps[operation.JsonPath] = timestamps;
        }

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>)) is not KeyValuePair<object, object?> payload)
        {
            return;
        }

        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null)
        {
            return;
        }
        
        if (timestamps.TryGetValue(itemKey, out var currentTimestamp) && operation.Timestamp.CompareTo(currentTimestamp) >= 0)
        {
            return;
        }

        timestamps[itemKey] = operation.Timestamp;

        switch (operation.Type)
        {
            case OperationType.Upsert:
                var itemValue = PocoPathHelper.ConvertValue(payload.Value, valueType);
                dict[itemKey] = itemValue;
                break;
            case OperationType.Remove:
                dict.Remove(itemKey);
                break;
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        if (data is null || partitionableProperty is null) return null;

        if (PocoPathHelper.GetAccessor(partitionableProperty).Getter(data) is IDictionary dict && dict.Count > 0)
        {
            var keys = dict.Keys.Cast<object>().ToList();
            if (keys.FirstOrDefault() is not IComparable)
            {
                throw new InvalidOperationException($"The dictionary key of a partitionable FWW Map must implement IComparable.");
            }

            var comparableKeys = keys.Cast<IComparable>().ToList();
            comparableKeys.Sort();
            return comparableKeys.FirstOrDefault();
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (operation.JsonPath == null || string.IsNullOrWhiteSpace(partitionablePropertyPath)) return null;

        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal))
        {
            return null;
        }

        // Fast path for when value natively matches
        if (operation.Value is KeyValuePair<object, object?> kvp && kvp.Key is IComparable compFast)
        {
            return compFast;
        }

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>)) is KeyValuePair<object, object?> payload)
        {
            if (payload.Key is IComparable comparableKey)
            {
                return comparableKey;
            }

            if (PocoPathHelper.ConvertValue(payload.Key, typeof(IComparable)) is IComparable convertedKey)
            {
                return convertedKey;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        if (partitionableProperty is null) throw new ArgumentNullException(nameof(partitionableProperty));

        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);

        if (keyType == typeof(string)) return string.Empty;
        if (keyType == typeof(int)) return int.MinValue;
        if (keyType == typeof(long)) return long.MinValue;
        if (keyType == typeof(short)) return short.MinValue;
        if (keyType == typeof(byte)) return byte.MinValue;
        if (keyType == typeof(Guid)) return Guid.Empty;
        if (keyType == typeof(DateTime)) return DateTime.MinValue;
        if (keyType == typeof(DateTimeOffset)) return DateTimeOffset.MinValue;

        if (keyType.IsValueType)
        {
            var defaultVal = Activator.CreateInstance(keyType);
            if (defaultVal is IComparable comp) return comp;
        }

        throw new NotSupportedException($"Cannot determine a minimum key for type {keyType}. Ensure the type has a sensible default value implementing IComparable.");
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        if (originalData is null) throw new ArgumentNullException(nameof(originalData));
        if (originalMetadata is null) throw new ArgumentNullException(nameof(originalMetadata));
        if (partitionableProperty is null) throw new ArgumentNullException(nameof(partitionableProperty));

        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.FwwMaps.TryGetValue(path, out var fwwMap) || fwwMap.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedEntries = fwwMap.ToList();
        sortedEntries.Sort((a, b) => ((IComparable)a.Key).CompareTo((IComparable)b.Key));

        var splitIndex = sortedEntries.Count / 2;
        var splitKey = (IComparable)sortedEntries[splitIndex].Key;

        var items1 = sortedEntries.Take(splitIndex).ToList();
        var items2 = sortedEntries.Skip(splitIndex).ToList();

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

        var comparer = comparerProvider.GetComparer(PocoPathHelper.GetDictionaryKeyType(partitionableProperty));
        
        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        meta1.FwwMaps[path] = items1.ToDictionary(k => k.Key, v => v.Value, comparer);
        meta2.FwwMaps[path] = items2.ToDictionary(k => k.Key, v => v.Value, comparer);

        ReconstructDictionaryForSplitMerge(doc1, path, items1, originalData);
        ReconstructDictionaryForSplitMerge(doc2, path, items2, originalData);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        if (data1 is null) throw new ArgumentNullException(nameof(data1));
        if (meta1 is null) throw new ArgumentNullException(nameof(meta1));
        if (data2 is null) throw new ArgumentNullException(nameof(data2));
        if (meta2 is null) throw new ArgumentNullException(nameof(meta2));
        if (partitionableProperty is null) throw new ArgumentNullException(nameof(partitionableProperty));

        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(keyType);
        
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var items1 = meta1.FwwMaps.TryGetValue(path, out var i1) ? i1 : new Dictionary<object, ICrdtTimestamp>(comparer);
        var items2 = meta2.FwwMaps.TryGetValue(path, out var i2) ? i2 : new Dictionary<object, ICrdtTimestamp>(comparer);

        var mergedItems = new Dictionary<object, ICrdtTimestamp>(comparer);
        foreach (var kvp in items1) mergedItems[kvp.Key] = kvp.Value;
        foreach (var kvp in items2)
        {
            if (!mergedItems.TryGetValue(kvp.Key, out var existingTs) || kvp.Value.CompareTo(existingTs) < 0)
            {
                mergedItems[kvp.Key] = kvp.Value;
            }
        }
        
        mergedMeta.FwwMaps[path] = mergedItems;

        var sortedItems = mergedItems.ToList();
        sortedItems.Sort((a, b) => ((IComparable)a.Key).CompareTo((IComparable)b.Key));
        
        ReconstructDictionaryForMerge(mergedDoc, path, sortedItems, data1, meta1, data2, meta2);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructDictionaryForSplitMerge(object root, string path, List<KeyValuePair<object, ICrdtTimestamp>> items, object originalData)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        var (origParent, origProperty, _) = PocoPathHelper.ResolvePath(originalData, path);
        
        if (parent is null || property is null || origParent is null || origProperty is null) return;

        var origDict = PocoPathHelper.GetAccessor(origProperty).Getter(origParent) as IDictionary;

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;
        PocoPathHelper.GetAccessor(property).Setter(parent, dict);

        if (origDict is null) return;

        foreach (var item in items)
        {
            var typedKey = PocoPathHelper.ConvertValue(item.Key, keyType);
            if (typedKey != null && origDict.Contains(typedKey))
            {
                dict[typedKey] = origDict[typedKey];
            }
        }
    }

    private static void ReconstructDictionaryForMerge(object root, string path, List<KeyValuePair<object, ICrdtTimestamp>> items, object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        var (parent1, property1, _) = PocoPathHelper.ResolvePath(data1, path);
        var (parent2, property2, _) = PocoPathHelper.ResolvePath(data2, path);

        if (parent is null || property is null) return;

        var dict1 = property1 != null ? PocoPathHelper.GetAccessor(property1).Getter(parent1) as IDictionary : null;
        var dict2 = property2 != null ? PocoPathHelper.GetAccessor(property2).Getter(parent2) as IDictionary : null;

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;
        PocoPathHelper.GetAccessor(property).Setter(parent, dict);

        meta1.FwwMaps.TryGetValue(path, out var items1);
        meta2.FwwMaps.TryGetValue(path, out var items2);

        foreach (var item in items)
        {
            var typedKey = PocoPathHelper.ConvertValue(item.Key, keyType);
            if (typedKey is null) continue;

            bool inDict1 = dict1 != null && dict1.Contains(typedKey);
            bool inDict2 = dict2 != null && dict2.Contains(typedKey);

            if (inDict1 && inDict2)
            {
                var ts1 = items1?.TryGetValue(item.Key, out var t1) == true ? t1 : null;
                var ts2 = items2?.TryGetValue(item.Key, out var t2) == true ? t2 : null;

                if (ts2 != null && ts1 != null && ts2.CompareTo(ts1) < 0)
                {
                    dict[typedKey] = dict2![typedKey];
                }
                else if (ts2 != null && ts1 == null)
                {
                    dict[typedKey] = dict2![typedKey];
                }
                else
                {
                    dict[typedKey] = dict1![typedKey];
                }
            }
            else if (inDict1)
            {
                dict[typedKey] = dict1![typedKey];
            }
            else if (inDict2)
            {
                dict[typedKey] = dict2![typedKey];
            }
        }
    }
}