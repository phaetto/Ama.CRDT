namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
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
using Ama.CRDT.Services;

/// <summary>
/// Implements the G-Set (Grow-Only Set) CRDT strategy.
/// In a G-Set, elements can only be added. Remove operations are ignored.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class GSetStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalList = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedList = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        
        var comparer = comparerProvider.GetComparer(elementType);

        var addedItems = modifiedList.Except(originalList, comparer).ToList();

        foreach (var item in addedItems)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, changeTimestamp));
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is AddIntent addIntent)
        {
            var elementType = PocoPathHelper.GetCollectionElementType(context.Property);
            var itemValue = PocoPathHelper.ConvertValue(addIntent.Value, elementType);

            return new CrdtOperation(
                Guid.NewGuid(),
                context.ReplicaId,
                context.JsonPath,
                OperationType.Upsert,
                itemValue,
                context.Timestamp);
        }

        throw new NotSupportedException($"The intent {context.Intent.GetType().Name} is not supported by {nameof(GSetStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type == OperationType.Remove)
        {
            return;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IList list) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var itemValue = PocoPathHelper.ConvertValue(operation.Value, elementType);

        if (itemValue is null) return;

        var comparer = comparerProvider.GetComparer(elementType);
        var currentItems = list.Cast<object>().ToList();
        
        if (!currentItems.Contains(itemValue, comparer))
        {
            currentItems.Add(itemValue);
            
            // Sort the items to ensure a deterministic order across all replicas.
            var sortedItems = currentItems.OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
            
            list.Clear();
            foreach (var item in sortedItems)
            {
                list.Add(item);
            }
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var list = partitionableProperty.GetValue(data) as IList;
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

        var list = partitionableProperty.GetValue(originalData) as IList;
        if (list == null || list.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedItems = list.Cast<object>().Select(x => x as IComparable ?? x?.ToString() as IComparable).Where(x => x != null).OrderBy(x => x).ToList();
        var splitIndex = sortedItems.Count / 2;
        var splitKey = sortedItems[splitIndex];

        var keys1 = sortedItems.Take(splitIndex).ToHashSet();
        var keys2 = sortedItems.Skip(splitIndex).ToHashSet();

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

        var elementType = PocoPathHelper.GetCollectionElementType(partitionableProperty);

        ReconstructListForSplitMerge(doc1, path, list, keys1, elementType);
        ReconstructListForSplitMerge(doc2, path, list, keys2, elementType);

        return new SplitResult(new PartitionContent(doc1, originalMetadata.DeepClone()), new PartitionContent(doc2, originalMetadata.DeepClone()), splitKey!);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var list1 = partitionableProperty.GetValue(data1) as IList;
        var list2 = partitionableProperty.GetValue(data2) as IList;

        var (parent, property, _) = PocoPathHelper.ResolvePath(mergedDoc, path);
        if (parent is not null && property is not null)
        {
            var elementType = PocoPathHelper.GetCollectionElementType(partitionableProperty);
            var comparer = comparerProvider.GetComparer(elementType);
            var mergedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;

            var allItems = new HashSet<object>(comparer);
            if (list1 != null) foreach (var item in list1) allItems.Add(item);
            if (list2 != null) foreach (var item in list2) allItems.Add(item);

            var sortedItems = allItems.OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
            foreach (var item in sortedItems) mergedList.Add(item);

            property.SetValue(parent, mergedList);
        }

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructListForSplitMerge(object root, string path, IList sourceList, HashSet<IComparable?> keysToKeep, Type elementType)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        if (parent is null || property is null) return;

        var list = property.GetValue(parent) as IList;
        if (list is null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            list = (IList)Activator.CreateInstance(listType)!;
            property.SetValue(parent, list);
        }

        list.Clear();
        foreach (var item in sourceList)
        {
            var comp = item as IComparable ?? item?.ToString() as IComparable;
            if (keysToKeep.Contains(comp))
            {
                var converted = PocoPathHelper.ConvertValue(item, elementType);
                if (converted != null) list.Add(converted);
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
}