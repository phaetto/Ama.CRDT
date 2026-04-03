namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;

/// <summary>
/// Implements the G-Set (Grow-Only Set) CRDT strategy.
/// In a G-Set, elements can only be added. Remove operations are ignored.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedType(typeof(ISet<>))]
[CrdtSupportedIntent(typeof(AddIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class GSetStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalList = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedList = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        
        var comparer = comparerProvider.GetComparer(elementType);

        var addedItems = modifiedList.Except(originalList, comparer).ToList();

        foreach (var item in addedItems)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, changeTimestamp, clock));
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is AddIntent addIntent)
        {
            var elementType = PocoPathHelper.GetTypeInfo(context.Property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
            var itemValue = PocoPathHelper.ConvertValue(addIntent.Value, elementType, aotContexts);

            return new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Upsert,
                itemValue,
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"The intent {context.Intent.GetType().Name} is not supported by {nameof(GSetStrategy)}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type == OperationType.Remove)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (parent is null || property is null || property.Getter?.Invoke(parent) is not IEnumerable collection)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var itemValue = PocoPathHelper.ConvertValue(operation.Value, elementType, aotContexts);

        if (itemValue is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var comparer = comparerProvider.GetComparer(elementType);
        var currentItems = collection.Cast<object>().ToList();
        
        if (!currentItems.Contains(itemValue, comparer))
        {
            PocoPathHelper.AddToCollection(collection, itemValue, aotContexts);
            
            // Sort the items to ensure a deterministic order across all replicas.
            var sortedItems = ((IEnumerable)collection).Cast<object>().OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
            
            PocoPathHelper.ClearCollection(collection, aotContexts);
            foreach (var item in sortedItems)
            {
                PocoPathHelper.AddToCollection(collection, item, aotContexts);
            }
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // GSetStrategy is an append-only collection that stores no metadata or tombstones.
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        var collection = partitionableProperty.Getter?.Invoke(data) as IEnumerable;
        if (collection == null) return null;

        var items = new List<IComparable>();
        foreach (var item in collection)
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
        return GetMinimumKeyForType(elementType, aotContexts);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var collection = partitionableProperty.Getter?.Invoke(originalData) as IEnumerable;
        if (collection == null)
        {
            throw new InvalidOperationException("Cannot split a null partition.");
        }

        var list = collection.Cast<object>().ToList();
        if (list.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedItems = list.Select(x => x as IComparable ?? x?.ToString() as IComparable).Where(x => x != null).OrderBy(x => x).ToList();
        var splitIndex = sortedItems.Count / 2;
        var splitKey = sortedItems[splitIndex];

        var keys1 = sortedItems.Take(splitIndex).ToHashSet();
        var keys2 = sortedItems.Skip(splitIndex).ToHashSet();

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts)!;

        var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);

        ReconstructListForSplitMerge(doc1, path, list, keys1, elementType, partitionableProperty.PropertyType, aotContexts);
        ReconstructListForSplitMerge(doc2, path, list, keys2, elementType, partitionableProperty.PropertyType, aotContexts);

        return new SplitResult(new PartitionContent(doc1, originalMetadata.DeepClone()), new PartitionContent(doc2, originalMetadata.DeepClone()), splitKey!);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = data1.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var list1 = partitionableProperty.Getter?.Invoke(data1) as IEnumerable;
        var list2 = partitionableProperty.Getter?.Invoke(data2) as IEnumerable;

        var (parent, property, _) = PocoPathHelper.ResolvePath(mergedDoc, path, aotContexts);
        if (parent is not null && property is not null)
        {
            var elementType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
            var comparer = comparerProvider.GetComparer(elementType);
            var mergedList = PocoPathHelper.InstantiateCollection(partitionableProperty.PropertyType, aotContexts);

            var allItems = new HashSet<object>(comparer);
            if (list1 != null) foreach (var item in list1) allItems.Add(item);
            if (list2 != null) foreach (var item in list2) allItems.Add(item);

            var sortedItems = allItems.OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
            foreach (var item in sortedItems) PocoPathHelper.AddToCollection(mergedList, item, aotContexts);

            property.Setter?.Invoke(parent, mergedList);
        }

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructListForSplitMerge(object root, string path, IEnumerable sourceList, HashSet<IComparable?> keysToKeep, Type elementType, Type propertyType, IEnumerable<CrdtAotContext> aotContexts)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path, aotContexts);
        if (parent is null || property is null) return;

        var collection = property.Getter?.Invoke(parent);
        if (collection is null)
        {
            collection = PocoPathHelper.InstantiateCollection(propertyType, aotContexts);
            property.Setter?.Invoke(parent, collection);
        }

        PocoPathHelper.ClearCollection(collection, aotContexts);
        foreach (var item in sourceList)
        {
            var comp = item as IComparable ?? item?.ToString() as IComparable;
            if (keysToKeep.Contains(comp))
            {
                var converted = PocoPathHelper.ConvertValue(item, elementType, aotContexts);
                if (converted != null) PocoPathHelper.AddToCollection(collection, converted, aotContexts);
            }
        }
    }

    private static IComparable GetMinimumKeyForType(Type keyType, IEnumerable<CrdtAotContext> aotContexts)
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