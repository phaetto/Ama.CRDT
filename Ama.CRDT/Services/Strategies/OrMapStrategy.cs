namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

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
            if (!Equals(originalDict![key], modifiedDict![key]))
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new OrMapAddItem(key, modifiedDict[key], Guid.NewGuid()), changeTimestamp));
            }
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
    public object? GetKeyFromOperation(CrdtOperation operation)
    {
        if (operation.Value is OrMapAddItem mapAdd)
        {
            return mapAdd.Key;
        }
        if (operation.Value is OrMapRemoveItem mapRemove)
        {
            return mapRemove.Key;
        }

        // This is a fallback for other operations that might not have a direct payload but still target a key,
        // for example, an LWW update on a value within the map.
        var path = operation.JsonPath;
        var keyMatch = Regex.Match(path, @"\[""(.+?)""\]|\[\'(.+?)\'\]");

        if (keyMatch.Success)
        {
            return keyMatch.Groups[1].Success ? keyMatch.Groups[1].Value : keyMatch.Groups[2].Value;
        }

        return null;
    }

    /// <inheritdoc/>
    public object? GetStartKey(object data)
    {
        var dictProperty = data.GetType().GetProperties().FirstOrDefault(p => typeof(IDictionary).IsAssignableFrom(p.PropertyType));
        if (dictProperty is null) return null;

        var dict = (IDictionary?)dictProperty.GetValue(data);
        return dict?.Keys.Count > 0 ? dict.Keys.Cast<object>().Min() : null;
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, Type documentType)
    {
        var dictProperty = FindDictionaryProperty(documentType);
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

        var (meta1, meta2) = SplitMetadata(dictProperty.Name, originalMetadata);

        var partition1Content = new PartitionContent(doc1, meta1);
        var partition2Content = new PartitionContent(doc2, meta2);
        return new SplitResult(partition1Content, partition2Content, splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, Type documentType)
    {
        var dictProperty = FindDictionaryProperty(documentType);
        var dict1 = (IDictionary)dictProperty.GetValue(data1)!;
        var dict2 = (IDictionary)dictProperty.GetValue(data2)!;

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var mergedDict = (IDictionary)Activator.CreateInstance(dict1.GetType())!;

        var path = $"$.{char.ToLowerInvariant(dictProperty.Name[0])}{dictProperty.Name[1..]}";

        var allKeys = dict1.Keys.Cast<object>().Union(dict2.Keys.Cast<object>()).Distinct();

        foreach (var key in allKeys)
        {
            var keyInDict1 = dict1.Contains(key);
            var keyInDict2 = dict2.Contains(key);

            var valuePath = $"{path}['{key.ToString()?.Replace("'", "\\'")}']";

            if (keyInDict1 && keyInDict2)
            {
                // Conflict, resolve with LWW
                meta1.Lww.TryGetValue(valuePath, out var ts1);
                meta2.Lww.TryGetValue(valuePath, out var ts2);

                if (ts1 != null && ts2 != null)
                {
                    if (ts1.CompareTo(ts2) >= 0)
                    {
                        mergedDict[key] = dict1[key];
                    }
                    else
                    {
                        mergedDict[key] = dict2[key];
                    }
                }
                else if (ts1 != null) // Only meta1 has timestamp, it wins
                {
                    mergedDict[key] = dict1[key];
                }
                else // Only meta2 has timestamp or neither have one. Default to dict2.
                {
                    mergedDict[key] = dict2[key];
                }
            }
            else if (keyInDict1)
            {
                mergedDict[key] = dict1[key];
            }
            else // keyInDict2
            {
                mergedDict[key] = dict2[key];
            }
        }

        dictProperty.SetValue(mergedDoc, mergedDict);

        var mergedMeta = MergeMetadata(dictProperty.Name, new[] { meta1, meta2 });

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private (CrdtMetadata, CrdtMetadata) SplitMetadata(string propertyName, CrdtMetadata original)
    {
        var path = $"$.{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";
        // For OR-Map, splitting is complex. The simplest, safe approach is to give each new partition a full copy of the original metadata.
        var newMeta1 = CloneMetadata(path, original);
        var newMeta2 = CloneMetadata(path, original);
        return (newMeta1, newMeta2);
    }

    private CrdtMetadata MergeMetadata(string propertyName, IEnumerable<CrdtMetadata> metadatas)
    {
        var path = $"$.{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";

        var mergedMetadata = new CrdtMetadata();
        var mergedAdds = new Dictionary<object, ISet<Guid>>();
        var mergedRemoves = new Dictionary<object, ISet<Guid>>();
        var mergedLww = new Dictionary<string, ICrdtTimestamp>();
        var mergedVersionVector = new Dictionary<string, ICrdtTimestamp>();
        var mergedSeenExceptions = new HashSet<CrdtOperation>();

        foreach (var metadata in metadatas)
        {
            if (metadata.OrMaps.TryGetValue(path, out var orMapState))
            {
                foreach (var (key, tags) in orMapState.Adds)
                {
                    if (!mergedAdds.TryGetValue(key, out var existingTags))
                    {
                        existingTags = new HashSet<Guid>();
                        mergedAdds[key] = existingTags;
                    }
                    foreach (var tag in tags) existingTags.Add(tag);
                }

                foreach (var (key, tags) in orMapState.Removes)
                {
                    if (!mergedRemoves.TryGetValue(key, out var existingTags))
                    {
                        existingTags = new HashSet<Guid>();
                        mergedRemoves[key] = existingTags;
                    }
                    foreach (var tag in tags) existingTags.Add(tag);
                }
            }

            foreach (var (lwwPath, timestamp) in metadata.Lww)
            {
                if (lwwPath.StartsWith(path, StringComparison.Ordinal))
                {
                    if (!mergedLww.TryGetValue(lwwPath, out var existingTimestamp) || timestamp.CompareTo(existingTimestamp) > 0)
                    {
                        mergedLww[lwwPath] = timestamp;
                    }
                }
            }
            
            mergedMetadata.VersionVector = mergedMetadata.VersionVector.Union(metadata.VersionVector)
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g.MaxBy(kvp => kvp.Value)!.Value);
            
            foreach (var exception in metadata.SeenExceptions) mergedSeenExceptions.Add(exception);
        }

        mergedMetadata.OrMaps[path] = new OrSetState(mergedAdds, mergedRemoves);
        foreach(var (lwwPath, timestamp) in mergedLww) mergedMetadata.Lww[lwwPath] = timestamp;
        mergedMetadata.SeenExceptions = mergedSeenExceptions;
        
        return mergedMetadata;
    }

    private static CrdtMetadata CloneMetadata(string path, CrdtMetadata original)
    {
        var newMeta = new CrdtMetadata
        {
            VersionVector = new Dictionary<string, ICrdtTimestamp>(original.VersionVector),
            SeenExceptions = new HashSet<CrdtOperation>(original.SeenExceptions)
        };

        if (original.OrMaps.TryGetValue(path, out var orMapState))
        {
            var newAdds = orMapState.Adds.ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value));
            var newRemoves = orMapState.Removes.ToDictionary(kvp => kvp.Key, kvp => (ISet<Guid>)new HashSet<Guid>(kvp.Value));
            newMeta.OrMaps[path] = new OrSetState(newAdds, newRemoves);
        }

        foreach (var (lwwPath, timestamp) in original.Lww)
        {
            if (lwwPath.StartsWith(path, StringComparison.Ordinal))
            {
                newMeta.Lww[lwwPath] = timestamp;
            }
        }
        
        return newMeta;
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
        var dictProperty = documentType.GetProperties().FirstOrDefault(p => typeof(IDictionary).IsAssignableFrom(p.PropertyType));
        if (dictProperty is null)
        {
            throw new NotSupportedException($"Type '{documentType.Name}' does not have a dictionary property for partitioning.");
        }
        return dictProperty;
    }
}