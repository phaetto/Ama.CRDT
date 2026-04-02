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

/// <summary>
/// Implements the FWW-Set (First-Writer-Wins Set) CRDT strategy.
/// An element's membership is determined by the timestamp of its earliest add or remove operation.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(RemoveValueIntent))]
[CrdtSupportedIntent(typeof(ClearIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class FwwSetStrategy(
    IElementComparerProvider comparerProvider,
    ICrdtTimestampProvider timestampProvider,
    ReplicaContext replicaContext,
    IEnumerable<CrdtContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalSet = originalValue as IEnumerable;
        var modifiedSet = modifiedValue as IEnumerable;
        
        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var originalHashSet = new HashSet<object>(comparer);
        if (originalSet != null)
        {
            foreach (var item in originalSet)
            {
                originalHashSet.Add(item);
            }
        }

        var modifiedHashSet = new HashSet<object>(comparer);
        if (modifiedSet != null)
        {
            foreach (var item in modifiedSet)
            {
                modifiedHashSet.Add(item);
            }
        }

        foreach (var item in modifiedHashSet)
        {
            if (!originalHashSet.Contains(item))
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, changeTimestamp, clock));
            }
        }

        foreach (var item in originalHashSet)
        {
            if (!modifiedHashSet.Contains(item))
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item, changeTimestamp, clock));
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        return context.Intent switch
        {
            AddIntent addIntent => new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Upsert, addIntent.Value, context.Timestamp, context.Clock),
            RemoveValueIntent removeIntent => new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Remove, removeIntent.Value, context.Timestamp, context.Clock),
            ClearIntent => new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Remove, null, context.Timestamp, context.Clock),
            _ => throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(FwwSetStrategy)}.")
        };
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var resolution = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        var parent = resolution.Parent;
        var property = resolution.Property;
        if (parent is null || property is null || property.Getter!(parent) is not IList list)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        bool isReset = operation.Type == OperationType.Remove && operation.Value is null;
        if (isReset)
        {
            list.Clear();
            metadata.FwwSets.Remove(operation.JsonPath);
            return CrdtOperationStatus.Success;
        }

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        if (!metadata.FwwSets.TryGetValue(operation.JsonPath, out var state))
        {
            var adds = new Dictionary<object, ICrdtTimestamp>(comparer);
            var rems = new Dictionary<object, CausalTimestamp>(comparer);
            for (int i = 0; i < list.Count; i++)
            {
                adds[list[i]] = timestampProvider.Create(0);
            }
            state = new LwwSetState(adds, rems);
            metadata.FwwSets[operation.JsonPath] = state;
        }

        var itemValue = PocoPathHelper.ConvertValue(operation.Value, elementType, aotContexts);
        if (itemValue is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (!state.Adds.TryGetValue(itemValue, out var existingAdd) || operation.Timestamp.CompareTo(existingAdd) < 0)
                {
                    state.Adds[itemValue] = operation.Timestamp;
                }
                break;
            case OperationType.Remove:
                if (!state.Removes.TryGetValue(itemValue, out var existingRemove) || operation.Timestamp.CompareTo(existingRemove.Timestamp) < 0)
                {
                    state.Removes[itemValue] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
                }
                break;
            default:
                return CrdtOperationStatus.StrategyApplicationFailed;
        }

        bool isLiveNow = false;
        if (state.Adds.TryGetValue(itemValue, out var finalAddTs))
        {
            if (!state.Removes.TryGetValue(itemValue, out var finalRemoveTs) || finalAddTs.CompareTo(finalRemoveTs.Timestamp) <= 0)
            {
                isLiveNow = true;
            }
        }

        if (isLiveNow)
        {
            InsertSorted(list, itemValue, comparer);
        }
        else
        {
            RemoveFromList(list, itemValue, comparer);
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        if (!context.Metadata.FwwSets.TryGetValue(context.PropertyPath, out var state)) return;

        var deadItemsToRemove = new List<object>();

        foreach (var kvp in state.Removes)
        {
            var item = kvp.Key;
            var removeTs = kvp.Value;

            if (state.Adds.TryGetValue(item, out var addTs))
            {
                // In FWW, the lowest timestamp wins. If addTs > removeTs.Timestamp, the Remove operation is older
                // and thus wins, meaning the item is mathematically dead.
                if (addTs.CompareTo(removeTs.Timestamp) > 0)
                {
                    if (context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: removeTs.Timestamp, ReplicaId: removeTs.ReplicaId, Version: removeTs.Clock)) && 
                        context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: addTs)))
                    {
                        deadItemsToRemove.Add(item);
                    }
                }
            }
            else
            {
                // Item is in Removes but not in Adds, so it's dead.
                if (context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: removeTs.Timestamp, ReplicaId: removeTs.ReplicaId, Version: removeTs.Clock)))
                {
                    deadItemsToRemove.Add(item);
                }
            }
        }

        foreach (var item in deadItemsToRemove)
        {
            state.Removes.Remove(item);
            state.Adds.Remove(item); // Ensure we also drop the Add timestamp so it doesn't resurrect.
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        var list = partitionableProperty.Getter!(data) as IList;
        if (list == null || list.Count == 0) return null;

        IComparable? minKey = null;
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var comp = item as IComparable ?? item?.ToString();
            
            if (comp != null)
            {
                if (minKey == null || comp.CompareTo(minKey) < 0)
                {
                    minKey = comp;
                }
            }
        }

        return minKey;
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal)) return null;

        return operation.Value as IComparable ?? operation.Value?.ToString() as IComparable;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty)
    {
        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        return GetMinimumKeyForType(elementType, aotContexts);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.FwwSets.TryGetValue(path, out var state) || state.Adds.Count + state.Removes.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var allKeysSet = new HashSet<IComparable>();
        foreach (var key in state.Adds.Keys) allKeysSet.Add((IComparable)key);
        foreach (var key in state.Removes.Keys) allKeysSet.Add((IComparable)key);

        var allKeys = new List<IComparable>(allKeysSet);
        allKeys.Sort();

        var splitIndex = allKeys.Count / 2;
        var splitKey = allKeys[splitIndex];

        var keys1 = new HashSet<IComparable>();
        for (int i = 0; i < splitIndex; i++)
        {
            keys1.Add(allKeys[i]);
        }

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds1 = new Dictionary<object, ICrdtTimestamp>(comparer);
        var adds2 = new Dictionary<object, ICrdtTimestamp>(comparer);
        foreach (var kvp in state.Adds)
        {
            if (keys1.Contains((IComparable)kvp.Key)) adds1[kvp.Key] = kvp.Value;
            else adds2[kvp.Key] = kvp.Value;
        }

        var rems1 = new Dictionary<object, CausalTimestamp>(comparer);
        var rems2 = new Dictionary<object, CausalTimestamp>(comparer);
        foreach (var kvp in state.Removes)
        {
            if (keys1.Contains((IComparable)kvp.Key)) rems1[kvp.Key] = kvp.Value;
            else rems2[kvp.Key] = kvp.Value;
        }

        meta1.FwwSets[path] = new LwwSetState(adds1, rems1);
        meta2.FwwSets[path] = new LwwSetState(adds2, rems2);

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts)!;

        ReconstructListForSplitMerge(doc1, path, meta1.FwwSets[path], elementType, aotContexts);
        ReconstructListForSplitMerge(doc2, path, meta2.FwwSets[path], elementType, aotContexts);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = data1.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds = new Dictionary<object, ICrdtTimestamp>(comparer);
        var rems = new Dictionary<object, CausalTimestamp>(comparer);

        if (meta1.FwwSets.TryGetValue(path, out var state1))
        {
            foreach (var kvp in state1.Adds) adds[kvp.Key] = kvp.Value;
            foreach (var kvp in state1.Removes) rems[kvp.Key] = kvp.Value;
        }
        if (meta2.FwwSets.TryGetValue(path, out var state2))
        {
            foreach (var kvp in state2.Adds)
            {
                if (!adds.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) < 0)
                    adds[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in state2.Removes)
            {
                if (!rems.TryGetValue(kvp.Key, out var existing) || kvp.Value.Timestamp.CompareTo(existing.Timestamp) < 0)
                    rems[kvp.Key] = kvp.Value;
            }
        }

        var mergedState = new LwwSetState(adds, rems);
        mergedMeta.FwwSets[path] = mergedState;

        ReconstructListForSplitMerge(mergedDoc, path, mergedState, elementType, aotContexts);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructListForSplitMerge(object root, string path, LwwSetState state, Type elementType, IEnumerable<CrdtContext> aotContexts)
    {
        var resolution = PocoPathHelper.ResolvePath(root, path, aotContexts);
        if (resolution.Parent == null || resolution.Property == null) return;
        
        var list = resolution.Property.Getter!(resolution.Parent) as IList;
        if (list is null)
        {
            list = (IList)PocoPathHelper.Instantiate(resolution.Property.PropertyType, aotContexts)!;
            resolution.Property.Setter!(resolution.Parent, list);
        }

        list.Clear();
        var liveItems = new List<object>();

        foreach (var kvp in state.Adds)
        {
            var item = kvp.Key;
            var addTs = kvp.Value;
            if (!state.Removes.TryGetValue(item, out var rmTs) || addTs.CompareTo(rmTs.Timestamp) <= 0)
            {
                var converted = PocoPathHelper.ConvertValue(item, elementType, aotContexts);
                if (converted != null) liveItems.Add(converted);
            }
        }

        liveItems.Sort((x, y) => string.CompareOrdinal(x.ToString(), y.ToString()));
        foreach (var item in liveItems) list.Add(item);
    }
    
    private static void InsertSorted(IList list, object item, IEqualityComparer<object> comparer)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (comparer.Equals(list[i], item)) return; // Already exists
        }

        var itemComp = item as IComparable;
        var itemStr = itemComp == null ? item.ToString() ?? string.Empty : null;

        for (int i = 0; i < list.Count; i++)
        {
            var current = list[i];

            if (itemComp != null && current is IComparable currentComp)
            {
                if (itemComp.CompareTo(currentComp) < 0)
                {
                    list.Insert(i, item);
                    return;
                }
            }
            else
            {
                var currentStr = current?.ToString() ?? string.Empty;
                if (string.CompareOrdinal(itemStr, currentStr) < 0)
                {
                    list.Insert(i, item);
                    return;
                }
            }
        }
        list.Add(item);
    }

    private static void RemoveFromList(IList list, object item, IEqualityComparer<object> comparer)
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

    private static IComparable GetMinimumKeyForType(Type keyType, IEnumerable<CrdtContext> aotContexts)
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
            return (IComparable)PocoPathHelper.GetDefaultValue(keyType, aotContexts)!;
        }

        throw new InvalidOperationException($"Cannot determine minimum key for type {keyType}.");
    }
}