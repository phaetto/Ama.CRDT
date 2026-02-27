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
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

/// <summary>
/// Implements the LSEQ (Log-structured Sequence) strategy. LSEQ assigns dense, ordered identifiers
/// to list elements, which allows for generating a new identifier between any two existing ones.
/// This avoids floating-point precision issues while providing a stable, convergent order.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[Commutative]
[Associative]
[Idempotent]
[OperationBased]
public sealed class LseqStrategy(
    IElementComparerProvider elementComparerProvider,
    ICrdtTimestampProvider timestampProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    private const int Base = 32;

    /// <inheritdoc />
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        if (originalValue is not IList originalList || modifiedValue is not IList modifiedList) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var comparer = elementComparerProvider.GetComparer(elementType);
        
        if (!originalMeta.LseqTrackers.TryGetValue(path, out var originalItems))
        {
            originalItems = new List<LseqItem>();
        }
        
        var originalItemsByValue = originalItems.ToDictionary(i => i.Value!, i => i, comparer!);
        var insertedItems = new Dictionary<object, LseqItem>(comparer);

        // Deletions
        foreach (var item in originalItems)
        {
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            if (!modifiedList.Cast<object>().Contains(item.Value, comparer))
            {
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item.Identifier, timestampProvider.Create(0));
                operations.Add(op);
            }
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        }

        // Insertions
        for (var i = 0; i < modifiedList.Count; i++)
        {
            var currentItem = modifiedList[i]!;
            if (originalItemsByValue.ContainsKey(currentItem))
            {
                continue;
            }

            // Find the identifier of the previous element in the modified list.
            LseqIdentifier? prevId = null;
            if (i > 0)
            {
                var prevItem = modifiedList[i - 1]!;
                if (originalItemsByValue.TryGetValue(prevItem, out var p))
                {
                    prevId = p.Identifier;
                }
                else if (insertedItems.TryGetValue(prevItem, out var ins))
                {
                    prevId = ins.Identifier;
                }
            }

            // Find the identifier of the next element in the original sequence
            // by looking ahead in the modified list for an element that already exists.
            LseqIdentifier? nextId = null;
            for (var j = i + 1; j < modifiedList.Count; j++)
            {
                if (originalItemsByValue.TryGetValue(modifiedList[j]!, out var nextItemMetaData))
                {
                    nextId = nextItemMetaData.Identifier;
                    break;
                }
            }

            var newId = GenerateIdentifierBetween(prevId, nextId, replicaId);
            var newItem = new LseqItem(newId, currentItem);
            insertedItems.Add(currentItem, newItem);

            var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, timestampProvider.Create(0));
            operations.Add(op);
        }
    }

    /// <inheritdoc />
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (!metadata.LseqTrackers.TryGetValue(operation.JsonPath, out var lseqItems))
        {
            lseqItems = new List<LseqItem>();
            metadata.LseqTrackers[operation.JsonPath] = lseqItems;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqItem)) is LseqItem newItem)
                {
                    if (!lseqItems.Any(i => i.Identifier.Equals(newItem.Identifier)))
                    {
                        lseqItems.Add(newItem);
                    }
                }
                break;
            case OperationType.Remove:
                if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqIdentifier)) is LseqIdentifier idToRemove)
                {
                    lseqItems.RemoveAll(i => i.Identifier.Equals(idToRemove));
                }
                break;
        }

        lseqItems.Sort((a, b) => a.Identifier.CompareTo(b.Identifier));
        ReconstructList(root, operation.JsonPath, lseqItems);
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        // For LSEQ, the true key is the LseqIdentifier, which is stored in metadata, not in the POCO elements directly.
        // Therefore, we cannot determine the exact start key from the raw data object alone.
        // Returning null instructs the PartitionManager to use the absolute minimum key for the initial partition.
        return null;
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal))
        {
            return null;
        }

        object? key = null;
        if (operation.Value is LseqItem item) key = item.Identifier;
        else if (operation.Value is LseqIdentifier id) key = id;
        else if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqItem)) is LseqItem convItem) key = convItem.Identifier;
        else if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqIdentifier)) is LseqIdentifier convId) key = convId;

        if (key is null) return null;
        if (key is IComparable comparableKey) return comparableKey;
    
        throw new InvalidOperationException($"The key of a partitionable Lseq must implement IComparable. Key: '{key}'");
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        return new LseqIdentifier(ImmutableList<LseqPathSegment>.Empty);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.LseqTrackers.TryGetValue(path, out var lseqItems) || lseqItems.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var splitIndex = lseqItems.Count / 2;
        var splitKey = lseqItems[splitIndex].Identifier;

        var items1 = lseqItems.Take(splitIndex).ToList();
        var items2 = lseqItems.Skip(splitIndex).ToList();

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

        var (meta1, meta2) = SplitMetadata(path, originalMetadata, items1, items2);

        ReconstructList(doc1, path, items1);
        ReconstructList(doc2, path, items2);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = Activator.CreateInstance(partitionableProperty.DeclaringType!)!;
        var mergedMeta = MergeMetadata(path, meta1, meta2);

        if (mergedMeta.LseqTrackers.TryGetValue(path, out var mergedItems))
        {
            ReconstructList(mergedDoc, path, mergedItems);
        }

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private LseqIdentifier GenerateIdentifierBetween(LseqIdentifier? prev, LseqIdentifier? next, string replicaId)
    {
        var p1 = prev?.Path ?? ImmutableList<LseqPathSegment>.Empty;
        var p2 = next?.Path ?? ImmutableList<LseqPathSegment>.Empty;

        var newPath = ImmutableList.CreateBuilder<LseqPathSegment>();
        var level = 0;

        while (true)
        {
            var head1 = level < p1.Count ? p1[level] : new LseqPathSegment(0, string.Empty);
            var head2 = level < p2.Count ? p2[level] : new LseqPathSegment(Base, string.Empty);

            var diff = head2.Position - head1.Position;
            if (diff > 1)
            {
                var newPos = head1.Position + 1;
                newPath.Add(new LseqPathSegment(newPos, replicaId));
                break;
            }

            var prefixNode = level < p1.Count ? p1[level] : head1;
            newPath.Add(prefixNode);
            level++;
        }

        return new LseqIdentifier(newPath.ToImmutable());
    }

    private static (CrdtMetadata, CrdtMetadata) SplitMetadata(string path, CrdtMetadata original, List<LseqItem> items1, List<LseqItem> items2)
    {
        var meta1 = new CrdtMetadata();
        var meta2 = new CrdtMetadata();

        CloneNonPartitionableMetadata(original, meta1);
        CloneNonPartitionableMetadata(original, meta2);

        meta1.LseqTrackers[path] = items1;
        meta2.LseqTrackers[path] = items2;

        return (meta1, meta2);
    }

    private static CrdtMetadata MergeMetadata(string path, CrdtMetadata meta1, CrdtMetadata meta2)
    {
        var merged = new CrdtMetadata();
        merged.VersionVector = meta1.VersionVector.Union(meta2.VersionVector)
            .GroupBy(kvp => kvp.Key).ToDictionary(g => g.Key, g => g.MaxBy(kvp => kvp.Value)!.Value);

        var items1 = meta1.LseqTrackers.TryGetValue(path, out var i1) ? i1 : new List<LseqItem>();
        var items2 = meta2.LseqTrackers.TryGetValue(path, out var i2) ? i2 : new List<LseqItem>();

        var mergedItemsDict = new Dictionary<LseqIdentifier, LseqItem>();
        foreach (var item in items1) mergedItemsDict[item.Identifier] = item;
        foreach (var item in items2) mergedItemsDict[item.Identifier] = item;

        var mergedItems = mergedItemsDict.Values.ToList();
        mergedItems.Sort((a, b) => a.Identifier.CompareTo(b.Identifier));

        merged.LseqTrackers[path] = mergedItems;

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
        destination.VersionVector = new Dictionary<string, long>(source.VersionVector);
        destination.SeenExceptions = new HashSet<CrdtOperation>(source.SeenExceptions);
        foreach (var kvp in source.Lww)
        {
            destination.Lww[kvp.Key] = kvp.Value;
        }
    }

    private static void ReconstructList(object root, string path, List<LseqItem> lseqItems)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        if (parent is null || property is null) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var list = property.GetValue(parent) as IList;
        
        if (list is null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            list = (IList)Activator.CreateInstance(listType)!;
            property.SetValue(parent, list);
        }

        list.Clear();
        foreach (var item in lseqItems)
        {
            list.Add(PocoPathHelper.ConvertValue(item.Value, elementType));
        }
    }
}