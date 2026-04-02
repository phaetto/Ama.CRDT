namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
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

/// <summary>
/// Implements the OR-Set (Observed-Remove Set) CRDT strategy.
/// This set allows re-addition of elements by assigning a unique tag to each added instance.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedType(typeof(ISet<>))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(RemoveValueIntent))]
[CrdtSupportedIntent(typeof(RemoveIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class OrSetStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext,
    IEnumerable<CrdtContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        var originalSet = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedSet = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        
        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var added = modifiedSet.Except(originalSet, comparer);
        var removed = originalSet.Except(modifiedSet, comparer);

        foreach (var item in added)
        {
            var payload = new OrSetAddItem(item, Guid.NewGuid());
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp, clock));
        }

        if (originalMeta.OrSets.TryGetValue(path, out var metaState))
        {
            foreach (var item in removed)
            {
                if (metaState.Adds.TryGetValue(item, out var tags) && tags.Count > 0)
                {
                    var payload = new OrSetRemoveItem(item, new HashSet<Guid>(tags));
                    operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, payload, changeTimestamp, clock));
                }
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.DocumentRoot is null) throw new ArgumentException("Document root cannot be null.", nameof(context));
        if (context.Metadata is null) throw new ArgumentException("Metadata cannot be null.", nameof(context));
        if (context.Property is null) throw new ArgumentException("Property cannot be null.", nameof(context));
        if (context.Intent is null) throw new ArgumentException("Intent cannot be null.", nameof(context));
        if (string.IsNullOrEmpty(context.JsonPath)) throw new ArgumentException("JsonPath cannot be null or empty.", nameof(context));
        if (context.Timestamp is null) throw new ArgumentException("Timestamp cannot be null.", nameof(context));

        var elementType = PocoPathHelper.GetTypeInfo(context.Property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);

        if (context.Intent is AddIntent addIntent)
        {
            var value = PocoPathHelper.ConvertValue(addIntent.Value, elementType, aotContexts);
            var payload = new OrSetAddItem(value!, Guid.NewGuid());
            return new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Upsert, payload, context.Timestamp, context.Clock);
        }

        if (context.Intent is RemoveValueIntent removeValueIntent)
        {
            var value = PocoPathHelper.ConvertValue(removeValueIntent.Value, elementType, aotContexts);
            return GenerateRemoveOperation(context, value);
        }

        if (context.Intent is RemoveIntent removeIntent)
        {
            var collection = PocoPathHelper.GetValue<IEnumerable>(context.DocumentRoot, context.JsonPath, aotContexts);
            if (collection is IList list)
            {
                if (removeIntent.Index >= 0 && removeIntent.Index < list.Count)
                {
                    var value = list[removeIntent.Index];
                    return GenerateRemoveOperation(context, value);
                }
                throw new ArgumentOutOfRangeException(nameof(removeIntent.Index), $"Index {removeIntent.Index} is out of range.");
            }
            throw new InvalidOperationException($"Could not resolve list at path {context.JsonPath} for RemoveIntent or it is not an ordered IList.");
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(OrSetStrategy)}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (parent is null || property is null || property.Getter!(parent) is not IEnumerable collection)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        if (!metadata.OrSets.TryGetValue(operation.JsonPath, out var state))
        {
            state = new OrSetState(new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer));
            metadata.OrSets[operation.JsonPath] = state;
        }

        object? modifiedItemValue = null;

        switch (operation.Type)
        {
            case OperationType.Upsert:
                modifiedItemValue = ApplyUpsert(state, operation.Value, elementType);
                break;
            case OperationType.Remove:
                modifiedItemValue = ApplyRemove(state, operation, elementType);
                break;
            default:
                return CrdtOperationStatus.StrategyApplicationFailed;
        }

        if (modifiedItemValue is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        bool isLiveNow = false;
        if (state.Adds.TryGetValue(modifiedItemValue, out var addTags))
        {
            if (!state.Removes.TryGetValue(modifiedItemValue, out var rmTags) || addTags.Except(rmTags.Keys).Any())
            {
                isLiveNow = true;
            }
        }

        if (isLiveNow)
        {
            InsertSorted(collection, modifiedItemValue, comparer);
        }
        else
        {
            RemoveFromCollection(collection, modifiedItemValue, comparer);
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        if (!context.Metadata.OrSets.TryGetValue(context.PropertyPath, out var state)) return;

        var itemsToRemove = new List<object>();

        foreach (var kvp in state.Removes)
        {
            var item = kvp.Key;
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
                if (state.Adds.TryGetValue(item, out var adds))
                {
                    foreach (var tag in safeToRemoveTags)
                    {
                        adds.Remove(tag);
                        removes.Remove(tag);
                    }
                    if (adds.Count == 0 && removes.Count == 0)
                    {
                        itemsToRemove.Add(item);
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
                        itemsToRemove.Add(item);
                    }
                }
            }
        }

        foreach (var item in itemsToRemove)
        {
            state.Adds.Remove(item);
            state.Removes.Remove(item);
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        var collection = PocoPathHelper.GetValue<IEnumerable>(data, partitionableProperty.Name, aotContexts);
        if (collection == null) return null;

        var items = new List<IComparable>();
        foreach (var item in collection)
        {
            if (item is IComparable c) items.Add(c);
            else if (item != null) items.Add(item.ToString()!);
        }
        if (items.Count == 0) return null;
        
        return items.OrderBy(k => k).FirstOrDefault();
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal)) return null;

        if (operation.Type == OperationType.Upsert)
        {
            var payload = PocoPathHelper.ConvertValue(operation.Value, typeof(OrSetAddItem), aotContexts);
            if (payload is OrSetAddItem addItem) return addItem.Value as IComparable ?? addItem.Value?.ToString() as IComparable;
        }
        else if (operation.Type == OperationType.Remove)
        {
            var payload = PocoPathHelper.ConvertValue(operation.Value, typeof(OrSetRemoveItem), aotContexts);
            if (payload is OrSetRemoveItem remItem) return remItem.Value as IComparable ?? remItem.Value?.ToString() as IComparable;
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty)
    {
        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        return GetMinimumKeyForType(elementType);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.OrSets.TryGetValue(path, out var state) || state.Adds.Count + state.Removes.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var allKeys = state.Adds.Keys.Union(state.Removes.Keys).Cast<IComparable>().OrderBy(k => k).ToList();
        var splitIndex = allKeys.Count / 2;
        var splitKey = allKeys[splitIndex];

        var keys1 = allKeys.Take(splitIndex).ToHashSet();
        var keys2 = allKeys.Skip(splitIndex).ToHashSet();

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds1 = new Dictionary<object, ISet<Guid>>(comparer);
        var adds2 = new Dictionary<object, ISet<Guid>>(comparer);
        foreach (var kvp in state.Adds)
        {
            if (keys1.Contains((IComparable)kvp.Key)) adds1[kvp.Key] = new HashSet<Guid>(kvp.Value);
            else adds2[kvp.Key] = new HashSet<Guid>(kvp.Value);
        }

        var rems1 = new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer);
        var rems2 = new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer);
        foreach (var kvp in state.Removes)
        {
            if (keys1.Contains((IComparable)kvp.Key)) rems1[kvp.Key] = new Dictionary<Guid, CausalTimestamp>(kvp.Value);
            else rems2[kvp.Key] = new Dictionary<Guid, CausalTimestamp>(kvp.Value);
        }

        meta1.OrSets[path] = new OrSetState(adds1, rems1);
        meta2.OrSets[path] = new OrSetState(adds2, rems2);

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts);
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts);

        ReconstructListForSplitMerge(doc1, path, meta1.OrSets[path], elementType, partitionableProperty.PropertyType);
        ReconstructListForSplitMerge(doc2, path, meta2.OrSets[path], elementType, partitionableProperty.PropertyType);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = data1.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts);
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds = new Dictionary<object, ISet<Guid>>(comparer);
        var rems = new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer);

        if (meta1.OrSets.TryGetValue(path, out var state1))
        {
            foreach (var kvp in state1.Adds) adds[kvp.Key] = new HashSet<Guid>(kvp.Value);
            foreach (var kvp in state1.Removes) rems[kvp.Key] = new Dictionary<Guid, CausalTimestamp>(kvp.Value);
        }
        if (meta2.OrSets.TryGetValue(path, out var state2))
        {
            foreach (var kvp in state2.Adds)
            {
                if (!adds.TryGetValue(kvp.Key, out var set)) { set = new HashSet<Guid>(); adds[kvp.Key] = set; }
                foreach (var t in kvp.Value) set.Add(t);
            }
            foreach (var kvp in state2.Removes)
            {
                if (!rems.TryGetValue(kvp.Key, out var dict)) { dict = new Dictionary<Guid, CausalTimestamp>(); rems[kvp.Key] = dict; }
                foreach (var t in kvp.Value) 
                {
                    if (!dict.TryGetValue(t.Key, out var existing) || t.Value.CompareTo(existing) > 0)
                    {
                        dict[t.Key] = t.Value;
                    }
                }
            }
        }

        var mergedState = new OrSetState(adds, rems);
        mergedMeta.OrSets[path] = mergedState;

        ReconstructListForSplitMerge(mergedDoc, path, mergedState, elementType, partitionableProperty.PropertyType);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private void ReconstructListForSplitMerge(object root, string path, OrSetState state, Type elementType, Type propertyType)
    {
        var collection = PocoPathHelper.GetValue<object>(root, path, aotContexts);
        if (collection is null)
        {
            collection = PocoPathHelper.InstantiateCollection(propertyType, aotContexts);
            PocoPathHelper.SetValue(root, path, collection, aotContexts);
        }

        PocoPathHelper.ClearCollection(collection, aotContexts);
        var liveItems = new List<object>();

        foreach (var kvp in state.Adds)
        {
            var item = kvp.Key;
            var addTags = kvp.Value;
            if (!state.Removes.TryGetValue(item, out var rmTags) || addTags.Except(rmTags.Keys).Any())
            {
                var converted = PocoPathHelper.ConvertValue(item, elementType, aotContexts);
                if (converted != null) liveItems.Add(converted);
            }
        }

        liveItems.Sort((x, y) => string.CompareOrdinal(x.ToString(), y.ToString()));
        foreach (var item in liveItems) PocoPathHelper.AddToCollection(collection, item, aotContexts);
    }
    
    private object? ApplyUpsert(OrSetState state, object? opValue, Type elementType)
    {
        if (PocoPathHelper.ConvertValue(opValue, typeof(OrSetAddItem), aotContexts) is not OrSetAddItem payload) return null;
        
        var itemValue = PocoPathHelper.ConvertValue(payload.Value, elementType, aotContexts);
        if (itemValue is null) return null;
        
        if (!state.Adds.TryGetValue(itemValue, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[itemValue] = addTags;
        }
        addTags.Add(payload.Tag);

        return itemValue;
    }

    private object? ApplyRemove(OrSetState state, CrdtOperation operation, Type elementType)
    {
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(OrSetRemoveItem), aotContexts) is not OrSetRemoveItem payload) return null;

        var itemValue = PocoPathHelper.ConvertValue(payload.Value, elementType, aotContexts);
        if (itemValue is null) return null;

        if (!state.Removes.TryGetValue(itemValue, out var removeDict))
        {
            removeDict = new Dictionary<Guid, CausalTimestamp>();
            state.Removes[itemValue] = removeDict;
        }
        var causalTimestamp = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
        foreach (var tag in payload.Tags)
        {
            if (!removeDict.TryGetValue(tag, out var existing) || causalTimestamp.CompareTo(existing) > 0)
            {
                removeDict[tag] = causalTimestamp;
            }
        }

        return itemValue;
    }

    private void InsertSorted(object collection, object item, IEqualityComparer<object> comparer)
    {
        if (collection is IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (comparer.Equals(list[i], item)) return; // Already exists
            }

            var itemStr = item.ToString() ?? string.Empty;
            for (int i = 0; i < list.Count; i++)
            {
                var currentStr = list[i]?.ToString() ?? string.Empty;
                if (string.CompareOrdinal(itemStr, currentStr) < 0)
                {
                    list.Insert(i, item);
                    return;
                }
            }
            list.Add(item);
        }
        else
        {
            var enumCol = ((IEnumerable)collection).Cast<object>();
            if (!enumCol.Contains(item, comparer))
            {
                PocoPathHelper.AddToCollection(collection, item, aotContexts);
            }
        }
    }

    private void RemoveFromCollection(object collection, object item, IEqualityComparer<object> comparer)
    {
        if (collection is IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (comparer.Equals(list[i], item))
                {
                    list.RemoveAt(i);
                    return;
                }
            }
        }
        else
        {
            PocoPathHelper.RemoveFromCollection(collection, item, aotContexts);
        }
    }

    private IComparable GetMinimumKeyForType(Type keyType)
    {
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
            var defaultVal = PocoPathHelper.GetDefaultValue(keyType, aotContexts);
            if (defaultVal is IComparable comparable) return comparable;
        }

        throw new InvalidOperationException($"Cannot determine minimum key for type {keyType}.");
    }

    private CrdtOperation GenerateRemoveOperation(GenerateOperationContext context, object? value)
    {
        var tags = new HashSet<Guid>();

        if (value != null && context.Metadata.OrSets.TryGetValue(context.JsonPath, out var state))
        {
            if (state.Adds.TryGetValue(value, out var existingTags))
            {
                tags = new HashSet<Guid>(existingTags);
            }
        }

        var payload = new OrSetRemoveItem(value!, tags);
        return new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Remove, payload, context.Timestamp, context.Clock);
    }
}