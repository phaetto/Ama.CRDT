namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        var originalDict = originalValue as IDictionary;
        var modifiedDict = modifiedValue as IDictionary;

        if (originalDict is null && modifiedDict is null) return;

        var keyType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);

        var originalKeys = originalDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);
        var modifiedKeys = modifiedDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);
        
        var addedKeys = modifiedKeys.Except(originalKeys, comparer);
        var removedKeys = originalKeys.Except(modifiedKeys, comparer);
        var commonKeys = originalKeys.Intersect(modifiedKeys, comparer);

        var timestamps = originalMeta.FwwMaps.TryGetValue(path, out var ts) ? ts : null;

        foreach (var key in addedKeys)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new KeyValuePair<object, object?>(key, modifiedDict![key]), changeTimestamp, clock));
        }

        foreach (var key in removedKeys)
        {
            if (timestamps != null && timestamps.TryGetValue(key, out var causal) && changeTimestamp.CompareTo(causal.Timestamp) >= 0)
            {
                continue;
            }
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new KeyValuePair<object, object?>(key, null), changeTimestamp, clock));
        }

        foreach (var key in commonKeys)
        {
            var originalItemValue = originalDict![key];
            var modifiedItemValue = modifiedDict![key];

            if (!Equals(originalItemValue, modifiedItemValue))
            {
                if (timestamps != null && timestamps.TryGetValue(key, out var causal) && changeTimestamp.CompareTo(causal.Timestamp) >= 0)
                {
                    continue;
                }
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
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var resolution = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        var parent = resolution.Parent;
        var property = resolution.Property;
        if (parent is null || property is null || property.Getter!(parent) is not IDictionary dict)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        bool isReset = operation.Type == OperationType.Remove && operation.Value is null;
        if (isReset)
        {
            dict.Clear();
            metadata.FwwMaps.Remove(operation.JsonPath);
            return CrdtOperationStatus.Success;
        }

        var typeInfo = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        var valueType = typeInfo.DictionaryValueType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);

        if (!metadata.FwwMaps.TryGetValue(operation.JsonPath, out var timestamps))
        {
            timestamps = new Dictionary<object, CausalTimestamp>(comparer);
            metadata.FwwMaps[operation.JsonPath] = timestamps;
        }

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>), aotContexts) is not KeyValuePair<object, object?> payload)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType, aotContexts);
        if (itemKey is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }
        
        if (timestamps.TryGetValue(itemKey, out var currentTimestamp) && operation.Timestamp.CompareTo(currentTimestamp.Timestamp) >= 0)
        {
            return CrdtOperationStatus.Obsolete;
        }

        timestamps[itemKey] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);

        switch (operation.Type)
        {
            case OperationType.Upsert:
                var itemValue = PocoPathHelper.ConvertValue(payload.Value, valueType, aotContexts);
                dict[itemKey] = itemValue;
                break;
            case OperationType.Remove:
                dict.Remove(itemKey);
                break;
            default:
                return CrdtOperationStatus.StrategyApplicationFailed;
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        if (context.Document is null) return;
        if (!context.Metadata.FwwMaps.TryGetValue(context.PropertyPath, out var timestamps)) return;

        var resolution = PocoPathHelper.ResolvePath(context.Document, context.PropertyPath, aotContexts);
        var parent = resolution.Parent;
        var property = resolution.Property;
        if (parent is null || property is null || property.Getter!(parent) is not IDictionary dict)
        {
            return;
        }

        var keysToRemove = new List<object>();

        foreach (var kvp in timestamps)
        {
            // If the key is not in the dictionary, it's a tombstone.
            if (!dict.Contains(kvp.Key) && context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: kvp.Value.Timestamp, ReplicaId: kvp.Value.ReplicaId, Version: kvp.Value.Clock)))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            timestamps.Remove(key);
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        if (data is null || partitionableProperty is null) return null;

        if (partitionableProperty.Getter!(data) is IDictionary dict && dict.Count > 0)
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

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>), aotContexts) is KeyValuePair<object, object?> payload)
        {
            if (payload.Key is IComparable comparableKey)
            {
                return comparableKey;
            }

            if (PocoPathHelper.ConvertValue(payload.Key, typeof(IComparable), aotContexts) is IComparable convertedKey)
            {
                return convertedKey;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty)
    {
        if (partitionableProperty is null) throw new ArgumentNullException(nameof(partitionableProperty));

        var keyType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object);

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
            var defaultVal = PocoPathHelper.GetDefaultValue(keyType, aotContexts);
            if (defaultVal is IComparable comp) return comp;
        }

        throw new NotSupportedException($"Cannot determine a minimum key for type {keyType}. Ensure the type has a sensible default value implementing IComparable.");
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        if (originalData is null) throw new ArgumentNullException(nameof(originalData));
        if (originalMetadata is null) throw new ArgumentNullException(nameof(originalMetadata));
        if (partitionableProperty is null) throw new ArgumentNullException(nameof(partitionableProperty));

        var documentType = originalData.GetType();
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

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts)!;

        var comparer = comparerProvider.GetComparer(PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object));
        
        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        meta1.FwwMaps[path] = items1.ToDictionary(k => k.Key, v => v.Value, comparer);
        meta2.FwwMaps[path] = items2.ToDictionary(k => k.Key, v => v.Value, comparer);

        ReconstructDictionaryForSplitMerge(doc1, path, items1, originalData, aotContexts);
        ReconstructDictionaryForSplitMerge(doc2, path, items2, originalData, aotContexts);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        if (data1 is null) throw new ArgumentNullException(nameof(data1));
        if (meta1 is null) throw new ArgumentNullException(nameof(meta1));
        if (data2 is null) throw new ArgumentNullException(nameof(data2));
        if (meta2 is null) throw new ArgumentNullException(nameof(meta2));
        if (partitionableProperty is null) throw new ArgumentNullException(nameof(partitionableProperty));

        var documentType = data1.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var keyType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);
        
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var items1 = meta1.FwwMaps.TryGetValue(path, out var i1) ? i1 : new Dictionary<object, CausalTimestamp>(comparer);
        var items2 = meta2.FwwMaps.TryGetValue(path, out var i2) ? i2 : new Dictionary<object, CausalTimestamp>(comparer);

        var mergedItems = new Dictionary<object, CausalTimestamp>(comparer);
        foreach (var kvp in items1) mergedItems[kvp.Key] = kvp.Value;
        foreach (var kvp in items2)
        {
            if (!mergedItems.TryGetValue(kvp.Key, out var existingTs) || kvp.Value.Timestamp.CompareTo(existingTs.Timestamp) < 0)
            {
                mergedItems[kvp.Key] = kvp.Value;
            }
        }
        
        mergedMeta.FwwMaps[path] = mergedItems;

        var sortedItems = mergedItems.ToList();
        sortedItems.Sort((a, b) => ((IComparable)a.Key).CompareTo((IComparable)b.Key));
        
        ReconstructDictionaryForMerge(mergedDoc, path, sortedItems, data1, meta1, data2, meta2, aotContexts);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructDictionaryForSplitMerge(object root, string path, List<KeyValuePair<object, CausalTimestamp>> items, object originalData, IEnumerable<CrdtAotContext> aotContexts)
    {
        var resolution = PocoPathHelper.ResolvePath(root, path, aotContexts);
        var parent = resolution.Parent;
        var property = resolution.Property;

        var origResolution = PocoPathHelper.ResolvePath(originalData, path, aotContexts);
        var origParent = origResolution.Parent;
        var origProperty = origResolution.Property;
        
        if (parent is null || property is null || origParent is null || origProperty is null) return;

        var origDict = origProperty.Getter!(origParent) as IDictionary;

        var typeInfo = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        
        var dict = property.Getter!(parent) as IDictionary;
        if (dict == null)
        {
            dict = (IDictionary)PocoPathHelper.Instantiate(property.PropertyType, aotContexts)!;
            property.Setter!(parent, dict);
        }

        if (origDict is null) return;

        foreach (var item in items)
        {
            var typedKey = PocoPathHelper.ConvertValue(item.Key, keyType, aotContexts);
            if (typedKey != null && origDict.Contains(typedKey))
            {
                dict[typedKey] = origDict[typedKey];
            }
        }
    }

    private static void ReconstructDictionaryForMerge(object root, string path, List<KeyValuePair<object, CausalTimestamp>> items, object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, IEnumerable<CrdtAotContext> aotContexts)
    {
        var resolution = PocoPathHelper.ResolvePath(root, path, aotContexts);
        var parent = resolution.Parent;
        var property = resolution.Property;

        var resolution1 = PocoPathHelper.ResolvePath(data1, path, aotContexts);
        var parent1 = resolution1.Parent;
        var property1 = resolution1.Property;

        var resolution2 = PocoPathHelper.ResolvePath(data2, path, aotContexts);
        var parent2 = resolution2.Parent;
        var property2 = resolution2.Property;

        if (parent is null || property is null) return;

        var dict1 = property1 != null && parent1 != null ? property1.Getter!(parent1) as IDictionary : null;
        var dict2 = property2 != null && parent2 != null ? property2.Getter!(parent2) as IDictionary : null;

        var typeInfo = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        
        var dict = property.Getter!(parent) as IDictionary;
        if (dict == null)
        {
            dict = (IDictionary)PocoPathHelper.Instantiate(property.PropertyType, aotContexts)!;
            property.Setter!(parent, dict);
        }

        meta1.FwwMaps.TryGetValue(path, out var items1);
        meta2.FwwMaps.TryGetValue(path, out var items2);

        foreach (var item in items)
        {
            var typedKey = PocoPathHelper.ConvertValue(item.Key, keyType, aotContexts);
            if (typedKey is null) continue;

            bool inDict1 = dict1 != null && dict1.Contains(typedKey);
            bool inDict2 = dict2 != null && dict2.Contains(typedKey);

            if (inDict1 && inDict2)
            {
                var ts1 = items1?.TryGetValue(item.Key, out var t1) == true ? (CausalTimestamp?)t1 : null;
                var ts2 = items2?.TryGetValue(item.Key, out var t2) == true ? (CausalTimestamp?)t2 : null;

                if (ts2 != null && ts1 != null && ts2.Value.Timestamp.CompareTo(ts1.Value.Timestamp) < 0)
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