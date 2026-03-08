namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
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

/// <summary>
/// Implements the LWW-Set (Last-Writer-Wins Set) CRDT strategy.
/// An element's membership is determined by the timestamp of its last add or remove operation.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedType(typeof(ISet<>))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(RemoveValueIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class LwwSetStrategy(
    IElementComparerProvider comparerProvider,
    ICrdtTimestampProvider timestampProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalSet = originalValue as IEnumerable;
        var modifiedSet = modifiedValue as IEnumerable;
        
        var elementType = PocoPathHelper.GetCollectionElementType(property);
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
            _ => throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(LwwSetStrategy)}.")
        };
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IEnumerable collection)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var comparer = comparerProvider.GetComparer(elementType);
        var listRepresentation = collection.Cast<object>().ToList();

        if (!metadata.LwwSets.TryGetValue(operation.JsonPath, out var state))
        {
            var adds = new Dictionary<object, ICrdtTimestamp>(comparer);
            for (int i = 0; i < listRepresentation.Count; i++)
            {
                adds[listRepresentation[i]] = timestampProvider.Create(0);
            }
            state = new LwwSetState(adds, new Dictionary<object, ICrdtTimestamp>(comparer));
            metadata.LwwSets[operation.JsonPath] = state;
        }

        var itemValue = PocoPathHelper.ConvertValue(operation.Value, elementType);
        if (itemValue is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (!state.Removes.TryGetValue(itemValue, out var removeTimestamp) || operation.Timestamp.CompareTo(removeTimestamp) > 0)
                {
                    if (!state.Adds.TryGetValue(itemValue, out var removeAddTimestamp) || operation.Timestamp.CompareTo(removeAddTimestamp) > 0)
                    {
                        state.Adds[itemValue] = operation.Timestamp;
                    }
                }
                break;
            case OperationType.Remove:
                if (!state.Adds.TryGetValue(itemValue, out var addTimestamp) || operation.Timestamp.CompareTo(addTimestamp) > 0)
                {
                    if (!state.Removes.TryGetValue(itemValue, out var addRemoveTimestamp) || operation.Timestamp.CompareTo(addRemoveTimestamp) > 0)
                    {
                        state.Removes[itemValue] = operation.Timestamp;
                    }
                }
                break;
            default:
                return CrdtOperationStatus.StrategyApplicationFailed;
        }

        // Incrementally update list instead of reconstructing it
        bool isLiveNow = false;
        if (state.Adds.TryGetValue(itemValue, out var finalAddTs))
        {
            if (!state.Removes.TryGetValue(itemValue, out var finalRemoveTs) || finalAddTs.CompareTo(finalRemoveTs) > 0)
            {
                isLiveNow = true;
            }
        }

        if (isLiveNow)
        {
            InsertSorted(collection, itemValue, comparer);
        }
        else
        {
            RemoveFromCollection(collection, itemValue, comparer);
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var collection = PocoPathHelper.GetValue<IEnumerable>(data, partitionableProperty.Name);
        if (collection == null) return null;

        IComparable? minKey = null;
        foreach (var item in collection)
        {
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
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        var elementType = PocoPathHelper.GetCollectionElementType(partitionableProperty);
        return GetMinimumKeyForType(elementType);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.LwwSets.TryGetValue(path, out var state) || state.Adds.Count + state.Removes.Count < 2)
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

        var elementType = PocoPathHelper.GetCollectionElementType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds1 = new Dictionary<object, ICrdtTimestamp>(comparer);
        var adds2 = new Dictionary<object, ICrdtTimestamp>(comparer);
        foreach (var kvp in state.Adds)
        {
            if (keys1.Contains((IComparable)kvp.Key)) adds1[kvp.Key] = kvp.Value;
            else adds2[kvp.Key] = kvp.Value;
        }

        var rems1 = new Dictionary<object, ICrdtTimestamp>(comparer);
        var rems2 = new Dictionary<object, ICrdtTimestamp>(comparer);
        foreach (var kvp in state.Removes)
        {
            if (keys1.Contains((IComparable)kvp.Key)) rems1[kvp.Key] = kvp.Value;
            else rems2[kvp.Key] = kvp.Value;
        }

        meta1.LwwSets[path] = new LwwSetState(adds1, rems1);
        meta2.LwwSets[path] = new LwwSetState(adds2, rems2);

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

        ReconstructListForSplitMerge(doc1, path, meta1.LwwSets[path], elementType, partitionableProperty.PropertyType);
        ReconstructListForSplitMerge(doc2, path, meta2.LwwSets[path], elementType, partitionableProperty.PropertyType);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var elementType = PocoPathHelper.GetCollectionElementType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds = new Dictionary<object, ICrdtTimestamp>(comparer);
        var rems = new Dictionary<object, ICrdtTimestamp>(comparer);

        if (meta1.LwwSets.TryGetValue(path, out var state1))
        {
            foreach (var kvp in state1.Adds) adds[kvp.Key] = kvp.Value;
            foreach (var kvp in state1.Removes) rems[kvp.Key] = kvp.Value;
        }
        if (meta2.LwwSets.TryGetValue(path, out var state2))
        {
            foreach (var kvp in state2.Adds)
            {
                if (!adds.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
                    adds[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in state2.Removes)
            {
                if (!rems.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
                    rems[kvp.Key] = kvp.Value;
            }
        }

        var mergedState = new LwwSetState(adds, rems);
        mergedMeta.LwwSets[path] = mergedState;

        ReconstructListForSplitMerge(mergedDoc, path, mergedState, elementType, partitionableProperty.PropertyType);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructListForSplitMerge(object root, string path, LwwSetState state, Type elementType, Type propertyType)
    {
        var collection = PocoPathHelper.GetValue<object>(root, path);
        if (collection is null)
        {
            collection = PocoPathHelper.InstantiateCollection(propertyType);
            PocoPathHelper.SetValue(root, path, collection);
        }

        PocoPathHelper.ClearCollection(collection);
        var liveItems = new List<object>();

        foreach (var kvp in state.Adds)
        {
            var item = kvp.Key;
            var addTs = kvp.Value;
            if (!state.Removes.TryGetValue(item, out var rmTs) || addTs.CompareTo(rmTs) > 0)
            {
                var converted = PocoPathHelper.ConvertValue(item, elementType);
                if (converted != null) liveItems.Add(converted);
            }
        }

        liveItems.Sort((x, y) => string.CompareOrdinal(x.ToString(), y.ToString()));
        foreach (var item in liveItems) PocoPathHelper.AddToCollection(collection, item);
    }
    
    private static void InsertSorted(object collection, object item, IEqualityComparer<object> comparer)
    {
        if (collection is IList list)
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
        else
        {
            var enumCol = ((IEnumerable)collection).Cast<object>();
            if (!enumCol.Contains(item, comparer))
            {
                PocoPathHelper.AddToCollection(collection, item);
            }
        }
    }

    private static void RemoveFromCollection(object collection, object item, IEqualityComparer<object> comparer)
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
            PocoPathHelper.RemoveFromCollection(collection, item);
        }
    }

    private static IComparable GetMinimumKeyForType(Type keyType)
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
            return (IComparable)Activator.CreateInstance(keyType)!;
        }

        throw new InvalidOperationException($"Cannot determine minimum key for type {keyType}.");
    }
}