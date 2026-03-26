namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Implements the OR-Map (Observed-Remove Map) CRDT strategy.
/// Key presence is managed using OR-Set logic, and value updates are handled with LWW logic.
/// </summary>
[CrdtSupportedType(typeof(IDictionary))]
[CrdtSupportedIntent(typeof(MapSetIntent))]
[CrdtSupportedIntent(typeof(MapRemoveIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class OrMapStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, nestedDiffs, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp, clock) = context;

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
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new OrMapAddItem(key, modifiedDict![key], Guid.NewGuid()), changeTimestamp, clock));
        }

        if (originalMeta.OrMaps.TryGetValue(path, out var metaState))
        {
            foreach (var key in removedKeys)
            {
                if (metaState.Adds.TryGetValue(key, out var tags) && tags.Any())
                {
                    operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new OrMapRemoveItem(key, new HashSet<Guid>(tags)), changeTimestamp, clock));
                }
            }
        }
        
        foreach (var key in commonKeys)
        {
            var originalItem = originalDict![key];
            var modifiedItem = modifiedDict![key];
            var itemPath = $"{path}['{key.ToString()?.Replace("'", "\\'")}']";
            nestedDiffs.Add(new DifferentiateObjectContext(
                Path: itemPath,
                Type: PocoPathHelper.GetDictionaryValueType(property),
                FromObj: originalItem,
                ToObj: modifiedItem,
                FromRoot: originalRoot,
                ToRoot: modifiedRoot,
                FromMeta: originalMeta,
                Operations: operations,
                ChangeTimestamp: changeTimestamp));
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (root, metadata, path, property, intent, timestamp, clock) = context;

        if (intent is MapSetIntent setIntent)
        {
            var keyType = PocoPathHelper.GetDictionaryKeyType(property);
            var valueType = PocoPathHelper.GetDictionaryValueType(property);
            var itemKey = PocoPathHelper.ConvertValue(setIntent.Key, keyType) ?? throw new ArgumentException($"Key cannot be null or incompatible for type {keyType.Name}.");
            var itemValue = PocoPathHelper.ConvertValue(setIntent.Value, valueType);

            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new OrMapAddItem(itemKey, itemValue, Guid.NewGuid()), timestamp, clock);
        }

        if (intent is MapRemoveIntent removeIntent)
        {
            var keyType = PocoPathHelper.GetDictionaryKeyType(property);
            var itemKey = PocoPathHelper.ConvertValue(removeIntent.Key, keyType) ?? throw new ArgumentException($"Key cannot be null or incompatible for type {keyType.Name}.");

            ISet<Guid> tags = new HashSet<Guid>();
            if (metadata.OrMaps.TryGetValue(path, out var state) && state.Adds.TryGetValue(itemKey, out var existingTags))
            {
                tags = new HashSet<Guid>(existingTags);
            }

            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new OrMapRemoveItem(itemKey, tags), timestamp, clock);
        }

        throw new NotSupportedException($"Explicit operation generation for intent '{intent.GetType().Name}' is not supported by {nameof(OrMapStrategy)}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IDictionary dict)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);
        var comparer = comparerProvider.GetComparer(keyType);

        if (!metadata.OrMaps.TryGetValue(operation.JsonPath, out var state))
        {
            state = new OrSetState(new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer));
            metadata.OrMaps[operation.JsonPath] = state;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (!ApplyUpsert(dict, metadata, state, operation, keyType, valueType))
                {
                    return CrdtOperationStatus.StrategyApplicationFailed;
                }
                break;
            case OperationType.Remove:
                var removedKey = ApplyRemove(state, operation, keyType);
                if (removedKey != null)
                {
                    if (!state.Adds.TryGetValue(removedKey, out var addTags) || 
                       (state.Removes.TryGetValue(removedKey, out var rmTags) && !addTags.Except(rmTags.Keys).Any()))
                    {
                        dict.Remove(removedKey);
                    }
                }
                else
                {
                    return CrdtOperationStatus.StrategyApplicationFailed;
                }
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

        var (parent, property, _) = PocoPathHelper.ResolvePath(context.Document, context.PropertyPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IDictionary dict)
        {
            return;
        }

        // 1. Prune LWW metadata for values of removed keys
        var prefix = context.PropertyPath + "['";
        var lwwKeysToRemove = new List<string>();

        foreach (var kvp in context.Metadata.Lww)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal) && kvp.Key.EndsWith("']", StringComparison.Ordinal))
            {
                bool exists = false;
                var extractedKeyStr = kvp.Key.Substring(prefix.Length, kvp.Key.Length - prefix.Length - 2).Replace("\\'", "'");
                
                foreach (var dictKey in dict.Keys)
                {
                    if (dictKey?.ToString() == extractedKeyStr)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists && context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: kvp.Value.Timestamp, ReplicaId: kvp.Value.ReplicaId, Version: kvp.Value.Clock)))
                {
                    lwwKeysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var key in lwwKeysToRemove)
        {
            context.Metadata.Lww.Remove(key);
        }

        // 2. OR-Map keys state (OrSetState) uses Guid tags.
        // We now have causal metadata on removals, so we can compact them!
        if (context.Metadata.OrMaps.TryGetValue(context.PropertyPath, out var state))
        {
            var keysToRemove = new List<object>();

            foreach (var kvp in state.Removes)
            {
                var key = kvp.Key;
                var removes = kvp.Value;
                var safeToRemoveTags = new List<Guid>();

                foreach (var (tag, causalTs) in removes)
                {
                    if (context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: causalTs.Timestamp, ReplicaId: causalTs.ReplicaId, Version: causalTs.Clock)))
                    {
                        safeToRemoveTags.Add(tag);
                    }
                }

                if (safeToRemoveTags.Count > 0)
                {
                    if (state.Adds.TryGetValue(key, out var adds))
                    {
                        foreach (var tag in safeToRemoveTags)
                        {
                            adds.Remove(tag);
                            removes.Remove(tag);
                        }
                        if (adds.Count == 0 && removes.Count == 0)
                        {
                            keysToRemove.Add(key);
                        }
                    }
                    else
                    {
                        foreach (var tag in safeToRemoveTags)
                        {
                            removes.Remove(tag);
                        }
                        if (removes.Count == 0)
                        {
                            keysToRemove.Add(key);
                        }
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                state.Adds.Remove(key);
                state.Removes.Remove(key);
            }
        }
    }
    
    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal))
        {
            return null;
        }

        object? key = null;
        if (operation.Value is OrMapAddItem mapAdd) key = mapAdd.Key;
        else if (operation.Value is OrMapRemoveItem mapRemove) key = mapRemove.Key;
        else if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrMapAddItem)) is OrMapAddItem convertedAdd) key = convertedAdd.Key;
        else if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrMapRemoveItem)) is OrMapRemoveItem convertedRemove) key = convertedRemove.Key;
        else 
        {
            var relativePath = operation.JsonPath[partitionablePropertyPath.Length..];
            if (relativePath.StartsWith("['") && relativePath.EndsWith("']"))
            {
                key = relativePath[2..^2].Replace("\\'", "'");
            }
        }

        if (key is null) return null;
        if (key is IComparable comparableKey) return comparableKey;
    
        throw new InvalidOperationException($"The key of a partitionable OR-Map must implement IComparable. Key: '{key}'");
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var dict = (IDictionary?)PocoPathHelper.GetAccessor(partitionableProperty).Getter(data);
        var key = dict is not null && dict.Count > 0 ? dict.Keys.Cast<object>().Min() : null;
        if (key is null) return null;
        if (key is IComparable comparableKey) return comparableKey;

        throw new InvalidOperationException($"The key of a partitionable OR-Map must implement IComparable. Key: '{key}'");
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);

        if (keyType == typeof(string))
        {
            return string.Empty;
        }

        if (keyType == typeof(Guid))
        {
            return Guid.Empty;
        }

        if (keyType.IsValueType)
        {
            var minValueField = keyType.GetField("MinValue", BindingFlags.Public | BindingFlags.Static);
            if (minValueField != null && minValueField.GetValue(null) is IComparable comparable)
            {
                return comparable;
            }
        }

        throw new NotSupportedException($"Cannot determine a minimum value for partition key type '{keyType.Name}'. " +
                                        "This is required when initializing with an empty partitionable collection. " +
                                        "Either provide a non-empty collection on initialization or use a key type with a known minimum value " +
                                        "(e.g., int, long, string, DateTime, Guid).");
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var dict = (IDictionary)PocoPathHelper.GetAccessor(partitionableProperty).Getter(originalData)!;
        if (dict.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }
        
        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(keyType);

        var sortedKeys = dict.Keys.Cast<object>().OrderBy(k => k).ToList();
        var splitIndex = sortedKeys.Count / 2;
        var splitKey = sortedKeys[splitIndex];

        if (splitKey is not IComparable comparableSplitKey)
        {
            throw new InvalidOperationException($"The key of a partitionable OR-Map must implement IComparable. Key: '{splitKey}'");
        }

        var keys1 = sortedKeys.Take(splitIndex).ToHashSet(comparer);
        var keys2 = sortedKeys.Skip(splitIndex).ToHashSet(comparer);

        var doc1 = Activator.CreateInstance(documentType)!;
        var dict1 = (IDictionary)Activator.CreateInstance(dict.GetType())!;
        foreach (var key in keys1) dict1.Add(key, dict[key]);
        PocoPathHelper.GetAccessor(partitionableProperty).Setter(doc1, dict1);

        var doc2 = Activator.CreateInstance(documentType)!;
        var dict2 = (IDictionary)Activator.CreateInstance(dict.GetType())!;
        foreach (var key in keys2) dict2.Add(key, dict[key]);
        PocoPathHelper.GetAccessor(partitionableProperty).Setter(doc2, dict2);

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        if (originalMetadata.OrMaps.TryGetValue(path, out var orMapState))
        {
            IDictionary<object, ISet<Guid>> adds1 = orMapState.Adds.Where(kvp => keys1.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), comparer);
            IDictionary<object, IDictionary<Guid, CausalTimestamp>> removes1 = orMapState.Removes.Where(kvp => keys1.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (IDictionary<Guid, CausalTimestamp>)new Dictionary<Guid, CausalTimestamp>(kvp.Value), comparer);
            meta1.OrMaps[path] = new OrSetState(adds1, removes1);

            IDictionary<object, ISet<Guid>> adds2 = orMapState.Adds.Where(kvp => keys2.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), comparer);
            IDictionary<object, IDictionary<Guid, CausalTimestamp>> removes2 = orMapState.Removes.Where(kvp => keys2.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (IDictionary<Guid, CausalTimestamp>)new Dictionary<Guid, CausalTimestamp>(kvp.Value), comparer);
            meta2.OrMaps[path] = new OrSetState(adds2, removes2);
        }

        // Optimized LWW metadata splitting.
        var stringKeys1 = keys1.Select(k => k?.ToString()).ToHashSet();
        var stringKeys2 = keys2.Select(k => k?.ToString()).ToHashSet();

        var keysToRemoveFrom1 = new List<string>();
        var keysToRemoveFrom2 = new List<string>();

        foreach (var lwwPath in originalMetadata.Lww.Keys)
        {
            if (lwwPath.StartsWith(path, StringComparison.Ordinal) && lwwPath.Length > path.Length + 4 && lwwPath.Contains("['"))
            {
                var keyStr = lwwPath.Substring(path.Length + 2, lwwPath.Length - path.Length - 4).Replace("\\'", "'");
                if (stringKeys2.Contains(keyStr)) keysToRemoveFrom1.Add(lwwPath);
                if (stringKeys1.Contains(keyStr)) keysToRemoveFrom2.Add(lwwPath);
            }
        }
        
        foreach(var k in keysToRemoveFrom1) meta1.Lww.Remove(k);
        foreach(var k in keysToRemoveFrom2) meta2.Lww.Remove(k);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), comparableSplitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";
        
        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(keyType);

        var dict1 = (IDictionary)PocoPathHelper.GetAccessor(partitionableProperty).Getter(data1)!;
        var dict2 = (IDictionary)PocoPathHelper.GetAccessor(partitionableProperty).Getter(data2)!;

        var mergedDoc = Activator.CreateInstance(partitionableProperty.DeclaringType!)!;
        var mergedDict = (IDictionary)Activator.CreateInstance(dict1.GetType())!;

        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var orMap1 = meta1.OrMaps.TryGetValue(path, out var s1) ? s1 : new OrSetState(new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer));
        var orMap2 = meta2.OrMaps.TryGetValue(path, out var s2) ? s2 : new OrSetState(new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer));
        
        IDictionary<object, ISet<Guid>> mergedAdds = orMap1.Adds.ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), comparer);
        foreach(var(key, tags) in orMap2.Adds)
        {
            if (!mergedAdds.TryGetValue(key, out var existingTags))
            {
                mergedAdds[key] = new HashSet<Guid>(tags);
            }
            else 
            {
                foreach (var tag in tags) existingTags.Add(tag);
            }
        }

        IDictionary<object, IDictionary<Guid, CausalTimestamp>> mergedRemoves = orMap1.Removes.ToDictionary(kvp => kvp.Key, kvp => (IDictionary<Guid, CausalTimestamp>)new Dictionary<Guid, CausalTimestamp>(kvp.Value), comparer);
        foreach(var(key, tagsDict) in orMap2.Removes)
        {
            if (!mergedRemoves.TryGetValue(key, out var existingDict))
            {
                mergedRemoves[key] = new Dictionary<Guid, CausalTimestamp>(tagsDict);
            }
            else 
            {
                foreach (var (tag, ts) in tagsDict) 
                {
                    if (!existingDict.TryGetValue(tag, out var existingTs) || ts.CompareTo(existingTs) > 0)
                    {
                        existingDict[tag] = ts;
                    }
                }
            }
        }

        mergedMeta.OrMaps[path] = new OrSetState(mergedAdds, mergedRemoves);

        // Add all items from dict1 first
        foreach (DictionaryEntry entry in dict1)
        {
            mergedDict[entry.Key] = entry.Value;
        }
    
        // Then add/update items from dict2, using LWW to resolve conflicts
        foreach (DictionaryEntry entry in dict2)
        {
            var key = entry.Key;
            var value2 = entry.Value;
            var itemPath = $"{path}['{key.ToString()?.Replace("'", "\\'")}']";

            if (mergedDict.Contains(key))
            {
                // Conflict: key exists in both. Resolve with LWW.
                var hasTs1 = meta1.Lww.TryGetValue(itemPath, out var ts1);
                var hasTs2 = meta2.Lww.TryGetValue(itemPath, out var ts2);

                // If ts2 is present, and either ts1 is not present or ts2 is newer, update the value.
                if (hasTs2 && (!hasTs1 || ts2.CompareTo(ts1) > 0))
                {
                    mergedDict[key] = value2;
                }
            }
            else
            {
                // Key only in dict2, just add it.
                mergedDict[key] = value2;
            }
        }
    
        // Reconstruct dictionary based on merged OR-Set state to handle removals
        if (mergedMeta.OrMaps.TryGetValue(path, out var orState))
        {
            var liveKeys = new HashSet<object>(comparer);
            foreach (var (key, addTags) in orState.Adds)
            {
                if (!orState.Removes.TryGetValue(key, out var rmTags) || addTags.Except(rmTags.Keys).Any())
                {
                    liveKeys.Add(key);
                }
            }
            
            var currentKeys = mergedDict.Keys.Cast<object>().ToList();
            foreach (var key in currentKeys)
            {
                if (!liveKeys.Contains(key))
                {
                    mergedDict.Remove(key);
                }
            }
        }
    
        PocoPathHelper.GetAccessor(partitionableProperty).Setter(mergedDoc, mergedDict);

        return new PartitionContent(mergedDoc, mergedMeta);
    }
    
    private bool ApplyUpsert(IDictionary dict, CrdtMetadata metadata, OrSetState state, CrdtOperation operation, Type keyType, Type valueType)
    {
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrMapAddItem)) is not OrMapAddItem payload) return false;

        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null) return false;

        if (!state.Adds.TryGetValue(itemKey, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[itemKey] = addTags;
        }
        addTags.Add(payload.Tag);
        
        var valuePath = $"{operation.JsonPath}['{itemKey.ToString()?.Replace("'", "\\'")}']";
        var causalOpTs = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
        if (!metadata.Lww.TryGetValue(valuePath, out var currentTimestamp) || causalOpTs.CompareTo(currentTimestamp) > 0)
        {
            metadata.Lww[valuePath] = causalOpTs;
            var itemValue = PocoPathHelper.ConvertValue(payload.Value, valueType);
            dict[itemKey] = itemValue;
        }

        return true;
    }

    private static object? ApplyRemove(OrSetState state, CrdtOperation operation, Type keyType)
    {
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrMapRemoveItem)) is not OrMapRemoveItem payload) return null;

        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null) return null;

        if (!state.Removes.TryGetValue(itemKey, out var removeDict))
        {
            removeDict = new Dictionary<Guid, CausalTimestamp>();
            state.Removes[itemKey] = removeDict;
        }
        var causalTimestamp = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
        foreach (var tag in payload.Tags)
        {
            if (!removeDict.TryGetValue(tag, out var existing) || causalTimestamp.CompareTo(existing) > 0)
            {
                removeDict[tag] = causalTimestamp;
            }
        }

        return itemKey;
    }
}