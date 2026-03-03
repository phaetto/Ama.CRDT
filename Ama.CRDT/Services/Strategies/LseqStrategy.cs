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
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

/// <summary>
/// Implements the LSEQ (Log-structured Sequence) strategy. LSEQ assigns dense, ordered identifiers
/// to list elements, which allows for generating a new identifier between any two existing ones.
/// This avoids floating-point precision issues while providing a stable, convergent order.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(InsertIntent))]
[CrdtSupportedIntent(typeof(RemoveIntent))]
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
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, _) = context;

        if (originalValue is not IList originalList || modifiedValue is not IList modifiedList) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var comparer = elementComparerProvider.GetComparer(elementType);
        
        if (!originalMeta.LseqTrackers.TryGetValue(path, out var originalItems))
        {
            originalItems = new List<LseqItem>();
        }

        // 1. O(N) Optimization: Trim common prefix
        int start = 0;
        while (start < originalItems.Count && start < modifiedList.Count &&
               comparer!.Equals(originalItems[start].Value, modifiedList[start]))
        {
            start++;
        }

        // 2. O(N) Optimization: Trim common suffix
        int endOrig = originalItems.Count - 1;
        int endMod = modifiedList.Count - 1;
        while (endOrig >= start && endMod >= start &&
               comparer!.Equals(originalItems[endOrig].Value, modifiedList[endMod]))
        {
            endOrig--;
            endMod--;
        }

        // Slice arrays to compute LCS only on the changed middle section
        var slicedOrig = new List<LseqItem>(endOrig - start + 1);
        for (int i = start; i <= endOrig; i++) slicedOrig.Add(originalItems[i]);

        var slicedMod = new List<object?>(endMod - start + 1);
        for (int i = start; i <= endMod; i++) slicedMod.Add(modifiedList[i]);

        // Calculate LCS on the trimmed slices (handles duplicates safely)
        var lcsMatrix = new int[slicedOrig.Count + 1, slicedMod.Count + 1];
        for (var i = 1; i <= slicedOrig.Count; i++)
        {
            for (var j = 1; j <= slicedMod.Count; j++)
            {
                if (comparer!.Equals(slicedOrig[i - 1].Value, slicedMod[j - 1]))
                {
                    lcsMatrix[i, j] = lcsMatrix[i - 1, j - 1] + 1;
                }
                else
                {
                    lcsMatrix[i, j] = Math.Max(lcsMatrix[i - 1, j], lcsMatrix[i, j - 1]);
                }
            }
        }

        var matchedOriginal = new bool[slicedOrig.Count];
        var matchedModified = new bool[slicedMod.Count];
        var o = slicedOrig.Count;
        var m = slicedMod.Count;

        while (o > 0 && m > 0)
        {
            if (comparer!.Equals(slicedOrig[o - 1].Value, slicedMod[m - 1]))
            {
                matchedOriginal[o - 1] = true;
                matchedModified[m - 1] = true;
                o--;
                m--;
            }
            else if (lcsMatrix[o - 1, m] > lcsMatrix[o, m - 1])
            {
                o--;
            }
            else
            {
                m--;
            }
        }

        // Emit removals for elements no longer present in the slice
        for (var i = 0; i < slicedOrig.Count; i++)
        {
            if (!matchedOriginal[i])
            {
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, slicedOrig[i].Identifier, timestampProvider.Create(0));
                operations.Add(op);
            }
        }

        // Emit insertions for new elements in the slice
        LseqIdentifier? prevId = start > 0 ? originalItems[start - 1].Identifier : null;
        var origIdx = 0;

        for (var i = 0; i < slicedMod.Count; i++)
        {
            if (matchedModified[i])
            {
                while (!matchedOriginal[origIdx]) origIdx++;
                prevId = slicedOrig[origIdx].Identifier;
                origIdx++;
            }
            else
            {
                // Find nextId by looking ahead for the next matched item, or fallback to the suffix boundary
                LseqIdentifier? nextId = null;
                int lookAheadIdx = origIdx;
                while (lookAheadIdx < slicedOrig.Count && !matchedOriginal[lookAheadIdx]) lookAheadIdx++;
                
                if (lookAheadIdx < slicedOrig.Count)
                {
                    nextId = slicedOrig[lookAheadIdx].Identifier;
                }
                else if (endOrig + 1 < originalItems.Count)
                {
                    nextId = originalItems[endOrig + 1].Identifier;
                }

                var newId = GenerateIdentifierBetween(prevId, nextId, replicaId);
                var newItem = new LseqItem(newId, slicedMod[i]);
                
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, timestampProvider.Create(0));
                operations.Add(op);
                
                prevId = newId; // The newly inserted item becomes the predecessor for the next insertion
            }
        }
    }

    /// <inheritdoc />
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (string.IsNullOrEmpty(context.JsonPath))
        {
            throw new ArgumentException("JSON path cannot be null or empty.", nameof(context));
        }

        if (context.Intent is null)
        {
            throw new ArgumentNullException(nameof(context), "Intent cannot be null.");
        }

        if (!context.Metadata.LseqTrackers.TryGetValue(context.JsonPath, out var lseqItems))
        {
            lseqItems = new List<LseqItem>();
        }

        switch (context.Intent)
        {
            case AddIntent add:
            {
                var lastId = lseqItems.Count > 0 ? lseqItems[^1].Identifier : (LseqIdentifier?)null;
                var newId = GenerateIdentifierBetween(lastId, null, context.ReplicaId);
                
                return new CrdtOperation(Guid.NewGuid(), context.ReplicaId, context.JsonPath, OperationType.Upsert, new LseqItem(newId, add.Value), context.Timestamp);
            }
            case InsertIntent insert:
            {
                if (insert.Index < 0 || insert.Index > lseqItems.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(insert.Index), "Index is out of range.");
                }

                var prevId = insert.Index > 0 ? lseqItems[insert.Index - 1].Identifier : (LseqIdentifier?)null;
                var nextId = insert.Index < lseqItems.Count ? lseqItems[insert.Index].Identifier : (LseqIdentifier?)null;
                var newId = GenerateIdentifierBetween(prevId, nextId, context.ReplicaId);
                
                return new CrdtOperation(Guid.NewGuid(), context.ReplicaId, context.JsonPath, OperationType.Upsert, new LseqItem(newId, insert.Value), context.Timestamp);
            }
            case RemoveIntent remove:
            {
                if (remove.Index < 0 || remove.Index >= lseqItems.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(remove.Index), "Index is out of range.");
                }

                return new CrdtOperation(Guid.NewGuid(), context.ReplicaId, context.JsonPath, OperationType.Remove, lseqItems[remove.Index].Identifier, context.Timestamp);
            }
            default:
                throw new NotSupportedException($"Explicit operation generation for intent '{context.Intent.GetType().Name}' is not supported in {nameof(LseqStrategy)}.");
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

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var list = PocoPathHelper.GetAccessor(property).Getter(parent) as IList;

        if (list is null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            list = (IList)Activator.CreateInstance(listType)!;
            PocoPathHelper.GetAccessor(property).Setter(parent, list);
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqItem)) is LseqItem newItem)
                {
                    // O(1) Check for existing to ensure idempotency
                    var existingIdx = lseqItems.FindIndex(i => i.Identifier.Equals(newItem.Identifier));
                    if (existingIdx >= 0) return;

                    // O(N) Find insertion position to maintain sorted order
                    int insertPos = 0;
                    while (insertPos < lseqItems.Count && lseqItems[insertPos].Identifier.CompareTo(newItem.Identifier) < 0)
                    {
                        insertPos++;
                    }

                    // Incrementally update both tracker and list without full reconstruction
                    lseqItems.Insert(insertPos, newItem);
                    list.Insert(insertPos, PocoPathHelper.ConvertValue(newItem.Value, elementType));
                }
                break;
            case OperationType.Remove:
                if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqIdentifier)) is LseqIdentifier idToRemove)
                {
                    var removeIdx = lseqItems.FindIndex(i => i.Identifier.Equals(idToRemove));
                    if (removeIdx >= 0)
                    {
                        lseqItems.RemoveAt(removeIdx);
                        if (removeIdx < list.Count)
                        {
                            list.RemoveAt(removeIdx);
                        }
                    }
                }
                break;
        }

        metadata.LseqTrackers[operation.JsonPath] = lseqItems;
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
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

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        meta1.LseqTrackers[path] = items1;
        meta2.LseqTrackers[path] = items2;

        ReconstructListForSplitMerge(doc1, path, items1);
        ReconstructListForSplitMerge(doc2, path, items2);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = Activator.CreateInstance(partitionableProperty.DeclaringType!)!;
        
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var items1 = meta1.LseqTrackers.TryGetValue(path, out var i1) ? i1 : new List<LseqItem>();
        var items2 = meta2.LseqTrackers.TryGetValue(path, out var i2) ? i2 : new List<LseqItem>();

        var mergedItemsDict = new Dictionary<LseqIdentifier, LseqItem>();
        foreach (var item in items1) mergedItemsDict[item.Identifier] = item;
        foreach (var item in items2) mergedItemsDict[item.Identifier] = item;

        var mergedItems = mergedItemsDict.Values.ToList();
        mergedItems.Sort((a, b) => a.Identifier.CompareTo(b.Identifier));

        mergedMeta.LseqTrackers[path] = mergedItems;

        ReconstructListForSplitMerge(mergedDoc, path, mergedItems);

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

    private static void ReconstructListForSplitMerge(object root, string path, List<LseqItem> lseqItems)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        if (parent is null || property is null) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var list = PocoPathHelper.GetAccessor(property).Getter(parent) as IList;
        
        if (list is null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            list = (IList)Activator.CreateInstance(listType)!;
            PocoPathHelper.GetAccessor(property).Setter(parent, list);
        }

        list.Clear();
        foreach (var item in lseqItems)
        {
            list.Add(PocoPathHelper.ConvertValue(item.Value, elementType));
        }
    }
}