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
/// Implements the OR-Set (Observed-Remove Set) CRDT strategy.
/// This set allows re-addition of elements by assigning a unique tag to each added instance.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(RemoveValueIntent))]
[CrdtSupportedIntent(typeof(RemoveIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class OrSetStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        var originalSet = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedSet = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        
        var elementType = PocoPathHelper.GetCollectionElementType(property);
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

        var elementType = PocoPathHelper.GetCollectionElementType(context.Property);

        if (context.Intent is AddIntent addIntent)
        {
            var value = PocoPathHelper.ConvertValue(addIntent.Value, elementType);
            var payload = new OrSetAddItem(value!, Guid.NewGuid());
            return new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Upsert, payload, context.Timestamp, context.Clock);
        }

        if (context.Intent is RemoveValueIntent removeValueIntent)
        {
            var value = PocoPathHelper.ConvertValue(removeValueIntent.Value, elementType);
            return GenerateRemoveOperation(context, value);
        }

        if (context.Intent is RemoveIntent removeIntent)
        {
            var list = PocoPathHelper.GetValue<IList>(context.DocumentRoot, context.JsonPath);
            if (list != null)
            {
                if (removeIntent.Index >= 0 && removeIntent.Index < list.Count)
                {
                    var value = list[removeIntent.Index];
                    return GenerateRemoveOperation(context, value);
                }
                throw new ArgumentOutOfRangeException(nameof(removeIntent.Index), $"Index {removeIntent.Index} is out of range.");
            }
            throw new InvalidOperationException($"Could not resolve list at path {context.JsonPath} for RemoveIntent.");
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(OrSetStrategy)}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IList list)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var comparer = comparerProvider.GetComparer(elementType);

        if (!metadata.OrSets.TryGetValue(operation.JsonPath, out var state))
        {
            state = new OrSetState(new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, ISet<Guid>>(comparer));
            metadata.OrSets[operation.JsonPath] = state;
        }

        object? modifiedItemValue = null;

        switch (operation.Type)
        {
            case OperationType.Upsert:
                modifiedItemValue = ApplyUpsert(state, operation.Value, elementType);
                break;
            case OperationType.Remove:
                modifiedItemValue = ApplyRemove(state, operation.Value, elementType);
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
            if (!state.Removes.TryGetValue(modifiedItemValue, out var rmTags) || addTags.Except(rmTags).Any())
            {
                isLiveNow = true;
            }
        }

        if (isLiveNow)
        {
            InsertSorted(list, modifiedItemValue, comparer);
        }
        else
        {
            RemoveFromList(list, modifiedItemValue, comparer);
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var list = PocoPathHelper.GetValue<IList>(data, partitionableProperty.Name);
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

        if (operation.Type == OperationType.Upsert)
        {
            var payload = PocoPathHelper.ConvertValue(operation.Value, typeof(OrSetAddItem));
            if (payload is OrSetAddItem addItem) return addItem.Value as IComparable ?? addItem.Value?.ToString() as IComparable;
        }
        else if (operation.Type == OperationType.Remove)
        {
            var payload = PocoPathHelper.ConvertValue(operation.Value, typeof(OrSetRemoveItem));
            if (payload is OrSetRemoveItem remItem) return remItem.Value as IComparable ?? remItem.Value?.ToString() as IComparable;
        }

        return null;
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

        var elementType = PocoPathHelper.GetCollectionElementType(partitionableProperty);
        var comparer = comparerProvider.GetComparer(elementType);

        var adds1 = new Dictionary<object, ISet<Guid>>(comparer);
        var adds2 = new Dictionary<object, ISet<Guid>>(comparer);
        foreach (var kvp in state.Adds)
        {
            if (keys1.Contains((IComparable)kvp.Key)) adds1[kvp.Key] = new HashSet<Guid>(kvp.Value);
            else adds2[kvp.Key] = new HashSet<Guid>(kvp.Value);
        }

        var rems1 = new Dictionary<object, ISet<Guid>>(comparer);
        var rems2 = new Dictionary<object, ISet<Guid>>(comparer);
        foreach (var kvp in state.Removes)
        {
            if (keys1.Contains((IComparable)kvp.Key)) rems1[kvp.Key] = new HashSet<Guid>(kvp.Value);
            else rems2[kvp.Key] = new HashSet<Guid>(kvp.Value);
        }

        meta1.OrSets[path] = new OrSetState(adds1, rems1);
        meta2.OrSets[path] = new OrSetState(adds2, rems2);

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

        ReconstructListForSplitMerge(doc1, path, meta1.OrSets[path], elementType, comparer);
        ReconstructListForSplitMerge(doc2, path, meta2.OrSets[path], elementType, comparer);

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

        var adds = new Dictionary<object, ISet<Guid>>(comparer);
        var rems = new Dictionary<object, ISet<Guid>>(comparer);

        if (meta1.OrSets.TryGetValue(path, out var state1))
        {
            foreach (var kvp in state1.Adds) adds[kvp.Key] = new HashSet<Guid>(kvp.Value);
            foreach (var kvp in state1.Removes) rems[kvp.Key] = new HashSet<Guid>(kvp.Value);
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
                if (!rems.TryGetValue(kvp.Key, out var set)) { set = new HashSet<Guid>(); rems[kvp.Key] = set; }
                foreach (var t in kvp.Value) set.Add(t);
            }
        }

        var mergedState = new OrSetState(adds, rems);
        mergedMeta.OrSets[path] = mergedState;

        ReconstructListForSplitMerge(mergedDoc, path, mergedState, elementType, comparer);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructListForSplitMerge(object root, string path, OrSetState state, Type elementType, IEqualityComparer<object> comparer)
    {
        var list = PocoPathHelper.GetValue<IList>(root, path);
        if (list is null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            list = (IList)Activator.CreateInstance(listType)!;
            PocoPathHelper.SetValue(root, path, list);
        }

        list.Clear();
        var liveItems = new List<object>();

        foreach (var kvp in state.Adds)
        {
            var item = kvp.Key;
            var addTags = kvp.Value;
            if (!state.Removes.TryGetValue(item, out var rmTags) || addTags.Except(rmTags).Any())
            {
                var converted = PocoPathHelper.ConvertValue(item, elementType);
                if (converted != null) liveItems.Add(converted);
            }
        }

        liveItems.Sort((x, y) => string.CompareOrdinal(x.ToString(), y.ToString()));
        foreach (var item in liveItems) list.Add(item);
    }
    
    private static object? ApplyUpsert(OrSetState state, object? opValue, Type elementType)
    {
        if (PocoPathHelper.ConvertValue(opValue, typeof(OrSetAddItem)) is not OrSetAddItem payload) return null;
        
        var itemValue = PocoPathHelper.ConvertValue(payload.Value, elementType);
        if (itemValue is null) return null;
        
        if (!state.Adds.TryGetValue(itemValue, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[itemValue] = addTags;
        }
        addTags.Add(payload.Tag);

        return itemValue;
    }

    private static object? ApplyRemove(OrSetState state, object? opValue, Type elementType)
    {
        if (PocoPathHelper.ConvertValue(opValue, typeof(OrSetRemoveItem)) is not OrSetRemoveItem payload) return null;

        var itemValue = PocoPathHelper.ConvertValue(payload.Value, elementType);
        if (itemValue is null) return null;

        if (!state.Removes.TryGetValue(itemValue, out var removeTags))
        {
            removeTags = new HashSet<Guid>();
            state.Removes[itemValue] = removeTags;
        }
        foreach (var tag in payload.Tags)
        {
            removeTags.Add(tag);
        }

        return itemValue;
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