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
[Mergeable]
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
    public object? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal))
        {
            // This operation is for a header field, not the partitionable dictionary.
            return null;
        }

        if (operation.Value is OrMapAddItem mapAdd) return mapAdd.Key;
        if (operation.Value is OrMapRemoveItem mapRemove) return mapRemove.Key;
        
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrMapAddItem)) is OrMapAddItem convertedAdd) return convertedAdd.Key;
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrMapRemoveItem)) is OrMapRemoveItem convertedRemove) return convertedRemove.Key;

        // Fallback for LWW ops on dictionary values. The key is embedded in the path.
        var relativePath = operation.JsonPath[partitionablePropertyPath.Length..];
        if (relativePath.StartsWith("['") && relativePath.EndsWith("']"))
        {
            return relativePath[2..^2].Replace("\\'", "'");
        }
        
        return null; // Should not happen for a well-formed path.
    }

    /// <inheritdoc/>
    public object? GetStartKey(object data)
    {
        var dictProperty = FindDictionaryProperty(data.GetType());
        var dict = (IDictionary?)dictProperty.GetValue(data);
        return dict is not null && dict.Count > 0 ? dict.Keys.Cast<object>().Min() : null;
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, Type documentType)
    {
        var dictProperty = FindDictionaryProperty(documentType);
        var path = $"$.{char.ToLowerInvariant(dictProperty.Name[0])}{dictProperty.Name[1..]}";

        var dict = (IDictionary)dictProperty.GetValue(originalData)!;
        if (dict.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedKeys = dict.Keys.Cast<object>().OrderBy(k => k).ToList();
        var splitIndex = sortedKeys.Count / 2;
        var splitKey = sortedKeys[splitIndex];

        var doc1 = Activator.CreateInstance(documentType)!;
        var dict1 = (IDictionary)Activator.CreateInstance(dict.GetType())!;
        foreach (var key in sortedKeys.Take(splitIndex)) dict1.Add(key, dict[key]);
        dictProperty.SetValue(doc1, dict1);

        var doc2 = Activator.CreateInstance(documentType)!;
        var dict2 = (IDictionary)Activator.CreateInstance(dict.GetType())!;
        foreach (var key in sortedKeys.Skip(splitIndex)) dict2.Add(key, dict[key]);
        dictProperty.SetValue(doc2, dict2);

        var (meta1, meta2) = SplitMetadata(path, originalMetadata, sortedKeys.Take(splitIndex).ToHashSet(), sortedKeys.Skip(splitIndex).ToHashSet());

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, Type documentType)
    {
        var dictProperty = FindDictionaryProperty(documentType);
        var path = $"$.{char.ToLowerInvariant(dictProperty.Name[0])}{dictProperty.Name[1..]}";
        
        var dict1 = (IDictionary)dictProperty.GetValue(data1)!;
        var dict2 = (IDictionary)dictProperty.GetValue(data2)!;

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var mergedDict = (IDictionary)Activator.CreateInstance(dict1.GetType())!;

        var mergedMeta = MergeMetadata(path, meta1, meta2);

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
            var keyType = PocoPathHelper.GetDictionaryKeyType(dictProperty);
            var comparer = comparerProvider.GetComparer(keyType);
            ReconstructDictionary(mergedDict, orState.Adds, orState.Removes, comparer);
        }
    
        dictProperty.SetValue(mergedDoc, mergedDict);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static (CrdtMetadata, CrdtMetadata) SplitMetadata(string path, CrdtMetadata original, ISet<object> keys1, ISet<object> keys2)
    {
        var meta1 = new CrdtMetadata();
        var meta2 = new CrdtMetadata();

        CloneNonPartitionableMetadata(original, meta1);
        CloneNonPartitionableMetadata(original, meta2);

        if (original.OrMaps.TryGetValue(path, out var orMapState))
        {
            // The OR-Set state must be copied in its entirety to both partitions.
            // It contains the history of all adds/removes needed for conflict resolution.
            meta1.OrMaps[path] = new OrSetState(
                new Dictionary<object, ISet<Guid>>(orMapState.Adds),
                new Dictionary<object, ISet<Guid>>(orMapState.Removes));
            meta2.OrMaps[path] = new OrSetState(
                new Dictionary<object, ISet<Guid>>(orMapState.Adds),
                new Dictionary<object, ISet<Guid>>(orMapState.Removes));
        }

        foreach (var (lwwPath, timestamp) in original.Lww)
        {
            if (lwwPath.StartsWith(path, StringComparison.Ordinal))
            {
                var keyStr = lwwPath[(path.Length + 2)..^2].Replace("\\'", "'");
                if (keys1.Any(k => k.ToString() == keyStr)) meta1.Lww[lwwPath] = timestamp;
                if (keys2.Any(k => k.ToString() == keyStr)) meta2.Lww[lwwPath] = timestamp;
            }
        }
        
        return (meta1, meta2);
    }

    private static CrdtMetadata MergeMetadata(string path, CrdtMetadata meta1, CrdtMetadata meta2)
    {
        var merged = new CrdtMetadata();
        // Merge non-partitionable metadata
        merged.VersionVector = meta1.VersionVector.Union(meta2.VersionVector)
            .GroupBy(kvp => kvp.Key).ToDictionary(g => g.Key, g => g.MaxBy(kvp => kvp.Value)!.Value);
        
        // Merge partitionable metadata
        var orMap1 = meta1.OrMaps.TryGetValue(path, out var s1) ? s1 : new OrSetState(new Dictionary<object, ISet<Guid>>(), new Dictionary<object, ISet<Guid>>());
        var orMap2 = meta2.OrMaps.TryGetValue(path, out var s2) ? s2 : new OrSetState(new Dictionary<object, ISet<Guid>>(), new Dictionary<object, ISet<Guid>>());
        
        IDictionary<object, ISet<Guid>> mergedAdds = orMap1.Adds.ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value));
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

        IDictionary<object, ISet<Guid>> mergedRemoves = orMap1.Removes.ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value));
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
        destination.VersionVector = new Dictionary<string, ICrdtTimestamp>(source.VersionVector);
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
    
    private static PropertyInfo FindDictionaryProperty(Type documentType)
    {
        var dictProperty = documentType.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<CrdtOrMapStrategyAttribute>() is not null);
        
        if (dictProperty is null)
        {
            throw new NotSupportedException($"Type '{documentType.Name}' does not have a dictionary property decorated with [CrdtOrMapStrategy] for partitioning.");
        }
        return dictProperty;
    }
}