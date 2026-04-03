namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.GarbageCollection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Implements the 2P-Set (Two-Phase Set) CRDT strategy.
/// In a 2P-Set, an element can be added and removed, but once removed, it cannot be re-added.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(RemoveValueIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class TwoPhaseSetStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalSet = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedSet = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        
        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var added = modifiedSet.Except(originalSet, comparer);
        var removed = originalSet.Except(modifiedSet, comparer);

        foreach (var item in added)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, changeTimestamp, clock));
        }

        foreach (var item in removed)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item, changeTimestamp, clock));
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (_, _, path, _, intent, timestamp, clock) = context;

        return intent switch
        {
            AddIntent add => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, add.Value, timestamp, clock),
            RemoveValueIntent remove => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, remove.Value, timestamp, clock),
            _ => throw new NotSupportedException($"Intent '{intent.GetType().Name}' is not supported by {nameof(TwoPhaseSetStrategy)}.")
        };
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (parent is null || property is null || property.Getter?.Invoke(parent) is not IList list)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        if (!metadata.TwoPhaseSets.TryGetValue(operation.JsonPath, out var state))
        {
            state = new TwoPhaseSetState(new HashSet<object>(comparer), new Dictionary<object, CausalTimestamp>(comparer));
            metadata.TwoPhaseSets[operation.JsonPath] = state;
        }

        var itemValue = PocoPathHelper.ConvertValue(operation.Value, elementType, aotContexts);
        if (itemValue is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (!state.Tombstones.ContainsKey(itemValue))
                {
                    state.Adds.Add(itemValue);
                }
                break;
            case OperationType.Remove:
                if (!state.Tombstones.TryGetValue(itemValue, out var existingTs) || operation.Timestamp.CompareTo(existingTs.Timestamp) > 0)
                {
                    state.Tombstones[itemValue] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
                }
                break;
            default:
                return CrdtOperationStatus.StrategyApplicationFailed;
        }

        bool isLiveNow = state.Adds.Contains(itemValue) && !state.Tombstones.ContainsKey(itemValue);

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
        if (context.Metadata.TwoPhaseSets.TryGetValue(context.PropertyPath, out var state))
        {
            var keysToRemove = new List<object>();

            foreach (var kvp in state.Tombstones)
            {
                var candidate = new CompactionCandidate(kvp.Value.Timestamp, kvp.Value.ReplicaId, kvp.Value.Clock);
                if (context.Policy.IsSafeToCompact(candidate))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                state.Tombstones.Remove(key);
                state.Adds.Remove(key);
            }
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        var list = PocoPathHelper.GetValue<IList>(data, partitionableProperty.Name, aotContexts);
        if (list == null || list.Count == 0) return null;

        var items = new List<IComparable>();
        foreach (var item in list)
        {
            if (item is IComparable c) items.Add(c);
            else if (item != null) items.Add(item.ToString()!);
        }
        return items.OrderBy(k => k).FirstOrDefault();
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
        return GetMinimumKeyForType(elementType);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();
        var path = $"$.{partitionableProperty.JsonName}";

        if (!originalMetadata.TwoPhaseSets.TryGetValue(path, out var state) || state.Adds.Count + state.Tombstones.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var allKeys = state.Adds.Union(state.Tombstones.Keys).Cast<IComparable>().OrderBy(k => k).ToList();
        var splitIndex = allKeys.Count / 2;
        var splitKey = allKeys[splitIndex];

        var keys1 = allKeys.Take(splitIndex).ToHashSet();
        var keys2 = allKeys.Skip(splitIndex).ToHashSet();

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds1 = new HashSet<object>(comparer);
        var adds2 = new HashSet<object>(comparer);
        foreach (var kvp in state.Adds)
        {
            if (keys1.Contains((IComparable)kvp)) adds1.Add(kvp);
            else adds2.Add(kvp);
        }

        var toms1 = new Dictionary<object, CausalTimestamp>(comparer);
        var toms2 = new Dictionary<object, CausalTimestamp>(comparer);
        foreach (var kvp in state.Tombstones)
        {
            if (keys1.Contains((IComparable)kvp.Key)) toms1.Add(kvp.Key, kvp.Value);
            else toms2.Add(kvp.Key, kvp.Value);
        }

        meta1.TwoPhaseSets[path] = new TwoPhaseSetState(adds1, toms1);
        meta2.TwoPhaseSets[path] = new TwoPhaseSetState(adds2, toms2);

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts);
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts);

        ReconstructListForSplitMerge(doc1, path, meta1.TwoPhaseSets[path], elementType, partitionableProperty.PropertyType);
        ReconstructListForSplitMerge(doc2, path, meta2.TwoPhaseSets[path], elementType, partitionableProperty.PropertyType);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = data1.GetType();
        var path = $"$.{partitionableProperty.JsonName}";

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts);
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds = new HashSet<object>(comparer);
        var toms = new Dictionary<object, CausalTimestamp>(comparer);

        if (meta1.TwoPhaseSets.TryGetValue(path, out var state1))
        {
            foreach (var item in state1.Adds) adds.Add(item);
            foreach (var item in state1.Tombstones) toms[item.Key] = item.Value;
        }
        if (meta2.TwoPhaseSets.TryGetValue(path, out var state2))
        {
            foreach (var item in state2.Adds) adds.Add(item);
            foreach (var item in state2.Tombstones) 
            {
                if (!toms.TryGetValue(item.Key, out var existing) || item.Value.CompareTo(existing) > 0)
                {
                    toms[item.Key] = item.Value;
                }
            }
        }

        var mergedState = new TwoPhaseSetState(adds, toms);
        mergedMeta.TwoPhaseSets[path] = mergedState;

        ReconstructListForSplitMerge(mergedDoc, path, mergedState, elementType, partitionableProperty.PropertyType);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private void ReconstructListForSplitMerge(object root, string path, TwoPhaseSetState state, Type elementType, Type propertyType)
    {
        var list = PocoPathHelper.GetValue<IList>(root, path, aotContexts);
        if (list is null)
        {
            list = (IList)PocoPathHelper.InstantiateCollection(propertyType, aotContexts);
            PocoPathHelper.SetValue(root, path, list, aotContexts);
        }

        list.Clear();
        var liveItems = new List<object>();

        foreach (var item in state.Adds)
        {
            if (!state.Tombstones.ContainsKey(item))
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
}