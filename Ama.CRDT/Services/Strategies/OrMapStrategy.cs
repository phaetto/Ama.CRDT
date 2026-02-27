namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System.Collections;
using System.Reflection;

/// <summary>
/// Implements the OR-Map (Observed-Remove Map) CRDT strategy.
/// Key presence is managed using OR-Set logic, and value updates are handled with LWW logic.
/// </summary>
[CrdtSupportedType(typeof(IDictionary))]
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
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

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
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new OrMapAddItem(key, modifiedDict![key], Guid.NewGuid()), changeTimestamp));
        }

        if (originalMeta.OrMaps.TryGetValue(path, out var metaState))
        {
            foreach (var key in removedKeys)
            {
                if (metaState.Adds.TryGetValue(key, out var tags) && tags.Any())
                {
                    operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new OrMapRemoveItem(key, new HashSet<Guid>(tags)), changeTimestamp));
                }
            }
        }
        
        foreach (var key in commonKeys)
        {
            var originalItem = originalDict![key];
            var modifiedItem = modifiedDict![key];
            var itemPath = $"{path}['{key.ToString()?.Replace("'", "\\'")}']";
            patcher.DifferentiateObject(new DifferentiateObjectContext(
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
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IDictionary dict) return;

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);
        var comparer = comparerProvider.GetComparer(keyType);

        if (!metadata.OrMaps.TryGetValue(operation.JsonPath, out var state))
        {
            state = new OrSetState(new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, ISet<Guid>>(comparer));
            metadata.OrMaps[operation.JsonPath] = state;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                ApplyUpsert(dict, metadata, state, operation, keyType, valueType);
                break;
            case OperationType.Remove:
                ApplyRemove(state, operation.Value, keyType);
                break;
        }

        ReconstructDictionary(dict, state.Adds, state.Removes, comparer);
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
        var dict = (IDictionary?)partitionableProperty.GetValue(data);
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

        var dict = (IDictionary)partitionableProperty.GetValue(originalData)!;
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
        partitionableProperty.SetValue(doc1, dict1);

        var doc2 = Activator.CreateInstance(documentType)!;
        var dict2 = (IDictionary)Activator.CreateInstance(dict.GetType())!;
        foreach (var key in keys2) dict2.Add(key, dict[key]);
        partitionableProperty.SetValue(doc2, dict2);

        var (meta1, meta2) = SplitMetadata(path, originalMetadata, keys1, keys2, comparer);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), comparableSplitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";
        
        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(keyType);

        var dict1 = (IDictionary)partitionableProperty.GetValue(data1)!;
        var dict2 = (IDictionary)partitionableProperty.GetValue(data2)!;

        var mergedDoc = Activator.CreateInstance(partitionableProperty.DeclaringType!)!;
        var mergedDict = (IDictionary)Activator.CreateInstance(dict1.GetType())!;

        var mergedMeta = MergeMetadata(path, meta1, meta2, comparer);

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
                meta1.Lww.TryGetValue(itemPath, out var ts1);
                meta2.Lww.TryGetValue(itemPath, out var ts2);

                // If ts2 is newer, update the value.
                if (ts2 is not null && (ts1 is null || ts2.CompareTo(ts1) > 0))
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
            ReconstructDictionary(mergedDict, orState.Adds, orState.Removes, comparer);
        }
    
        partitionableProperty.SetValue(mergedDoc, mergedDict);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static (CrdtMetadata, CrdtMetadata) SplitMetadata(string path, CrdtMetadata original, ISet<object> keys1, ISet<object> keys2, IEqualityComparer<object> keyComparer)
    {
        var meta1 = new CrdtMetadata();
        var meta2 = new CrdtMetadata();

        CloneNonPartitionableMetadata(original, meta1);
        CloneNonPartitionableMetadata(original, meta2);

        if (original.OrMaps.TryGetValue(path, out var orMapState))
        {
            // Partition the OR-Map state instead of duplicating it.
            // Each new partition only needs metadata for the keys it contains.
            IDictionary<object, ISet<Guid>> adds1 = orMapState.Adds.Where(kvp => keys1.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), keyComparer);
            IDictionary<object, ISet<Guid>> removes1 = orMapState.Removes.Where(kvp => keys1.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), keyComparer);
            meta1.OrMaps[path] = new OrSetState(adds1, removes1);

            IDictionary<object, ISet<Guid>> adds2 = orMapState.Adds.Where(kvp => keys2.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), keyComparer);
            IDictionary<object, ISet<Guid>> removes2 = orMapState.Removes.Where(kvp => keys2.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), keyComparer);
            meta2.OrMaps[path] = new OrSetState(adds2, removes2);
        }

        // Optimized LWW metadata splitting.
        var stringKeys1 = keys1.Select(k => k?.ToString()).ToHashSet();
        var stringKeys2 = keys2.Select(k => k?.ToString()).ToHashSet();

        foreach (var (lwwPath, timestamp) in original.Lww)
        {
            if (lwwPath.StartsWith(path, StringComparison.Ordinal) && lwwPath.Length > path.Length + 4 && lwwPath.Contains("['"))
            {
                var keyStr = lwwPath.Substring(path.Length + 2, lwwPath.Length - path.Length - 4).Replace("\\'", "'");
                if (stringKeys1.Contains(keyStr))
                {
                    meta1.Lww[lwwPath] = timestamp;
                }
                else if (stringKeys2.Contains(keyStr))
                {
                    meta2.Lww[lwwPath] = timestamp;
                }
            }
        }
        
        return (meta1, meta2);
    }

    private static CrdtMetadata MergeMetadata(string path, CrdtMetadata meta1, CrdtMetadata meta2, IEqualityComparer<object> keyComparer)
    {
        var merged = new CrdtMetadata();
        // Merge non-partitionable metadata
        merged.VersionVector = meta1.VersionVector.Union(meta2.VersionVector)
            .GroupBy(kvp => kvp.Key).ToDictionary(g => g.Key, g => g.MaxBy(kvp => kvp.Value)!.Value);
        
        // Merge partitionable metadata
        var orMap1 = meta1.OrMaps.TryGetValue(path, out var s1) ? s1 : new OrSetState(new Dictionary<object, ISet<Guid>>(keyComparer), new Dictionary<object, ISet<Guid>>(keyComparer));
        var orMap2 = meta2.OrMaps.TryGetValue(path, out var s2) ? s2 : new OrSetState(new Dictionary<object, ISet<Guid>>(keyComparer), new Dictionary<object, ISet<Guid>>(keyComparer));
        
        IDictionary<object, ISet<Guid>> mergedAdds = orMap1.Adds.ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), keyComparer);
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

        IDictionary<object, ISet<Guid>> mergedRemoves = orMap1.Removes.ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value), keyComparer);
        foreach(var(key, tags) in orMap2.Removes)
        {
            if (!mergedRemoves.TryGetValue(key, out var existingTags))
            {
                mergedRemoves[key] = new HashSet<Guid>(tags);
            }
            else 
            {
                foreach (var tag in tags) existingTags.Add(tag);
            }
        }

        merged.OrMaps[path] = new OrSetState(mergedAdds, mergedRemoves);

        foreach (var (lwwPath, ts) in meta1.Lww.Concat(meta2.Lww))
        {
            if (!merged.Lww.TryGetValue(lwwPath, out var existingTs) || ts.CompareTo(existingTs) > 0)
            {
                merged.Lww[lwwPath] = ts;
            }
        }
        
        return merged;
    }

    private static void CloneNonPartitionableMetadata(CrdtMetadata source, CrdtMetadata destination)
    {
        destination.VersionVector = new Dictionary<string, long>(source.VersionVector);
        destination.SeenExceptions = new HashSet<CrdtOperation>(source.SeenExceptions);
        // Only copy LWW entries that are NOT part of a partitionable collection, which we assume don't follow the collection path pattern.
        // This is a simplification; a more robust solution might need explicit marking of which LWW entries are "header" vs "data".
        foreach (var (key, value) in source.Lww.Where(kvp => !kvp.Key.Contains("['")))
        {
            destination.Lww[key] = value;
        }
    }
    
    private void ApplyUpsert(IDictionary dict, CrdtMetadata metadata, OrSetState state, CrdtOperation operation, Type keyType, Type valueType)
    {
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrMapAddItem)) is not OrMapAddItem payload) return;

        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null) return;

        if (!state.Adds.TryGetValue(itemKey, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[itemKey] = addTags;
        }
        addTags.Add(payload.Tag);
        
        var valuePath = $"{operation.JsonPath}['{itemKey.ToString()?.Replace("'", "\\'")}']";
        if (!metadata.Lww.TryGetValue(valuePath, out var currentTimestamp) || operation.Timestamp.CompareTo(currentTimestamp) > 0)
        {
            metadata.Lww[valuePath] = operation.Timestamp;
            var itemValue = PocoPathHelper.ConvertValue(payload.Value, valueType);
            dict[itemKey] = itemValue;
        }
    }

    private static void ApplyRemove(OrSetState state, object? opValue, Type keyType)
    {
        if (PocoPathHelper.ConvertValue(opValue, typeof(OrMapRemoveItem)) is not OrMapRemoveItem payload) return;

        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null) return;

        if (!state.Removes.TryGetValue(itemKey, out var removeTags))
        {
            removeTags = new HashSet<Guid>();
            state.Removes[itemKey] = removeTags;
        }
        foreach (var tag in payload.Tags)
        {
            removeTags.Add(tag);
        }
    }

    private static void ReconstructDictionary(IDictionary dict, IDictionary<object, ISet<Guid>> adds, IDictionary<object, ISet<Guid>> removes, IEqualityComparer<object> comparer)
    {
        var liveKeys = new HashSet<object>(comparer);
        foreach (var (key, addTags) in adds)
        {
            if (removes.TryGetValue(key, out var removeTags))
            {
                if (addTags.Except(removeTags).Any())
                {
                    liveKeys.Add(key);
                }
            }
            else
            {
                liveKeys.Add(key);
            }
        }

        var currentKeys = dict.Keys.Cast<object>().ToList();
        foreach (var key in currentKeys)
        {
            if (!liveKeys.Contains(key))
            {
                dict.Remove(key);
            }
        }
    }
}