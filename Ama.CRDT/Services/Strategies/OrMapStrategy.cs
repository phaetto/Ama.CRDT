namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Services;

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
            state = (new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, ISet<Guid>>(comparer));
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

    private void ApplyUpsert(IDictionary dict, CrdtMetadata metadata, (IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, CrdtOperation operation, Type keyType, Type valueType)
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
        
        var valuePath = $"{operation.JsonPath}.['{itemKey.ToString()?.Replace("'", "\\'")}']";
        if (!metadata.Lww.TryGetValue(valuePath, out var currentTimestamp) || operation.Timestamp.CompareTo(currentTimestamp) > 0)
        {
            metadata.Lww[valuePath] = operation.Timestamp;
            var itemValue = PocoPathHelper.ConvertValue(payload.Value, valueType);
            dict[itemKey] = itemValue;
        }
    }

    private static void ApplyRemove((IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, object? opValue, Type keyType)
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