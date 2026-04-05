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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    private const int Base = 32;

    /// <inheritdoc />
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        var originalList = originalValue as IList ?? Array.Empty<object>();
        var modifiedList = modifiedValue as IList ?? Array.Empty<object>();

        if (originalList.Count == 0 && modifiedList.Count == 0) return;

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = elementComparerProvider.GetComparer(elementType);
        
        if (!originalMeta.States.TryGetValue(path, out var state) || state is not LseqState lseqState)
        {
            lseqState = new LseqState(new List<LseqItem>());
        }
        var originalItems = lseqState.Trackers;

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
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, slicedOrig[i].Identifier, changeTimestamp, clock);
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
                
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, changeTimestamp, clock);
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

        if (!context.Metadata.States.TryGetValue(context.JsonPath, out var state) || state is not LseqState lseqState)
        {
            lseqState = new LseqState(new List<LseqItem>());
        }
        var lseqItems = lseqState.Trackers;

        switch (context.Intent)
        {
            case AddIntent add:
            {
                var lastId = lseqItems.Count > 0 ? lseqItems[^1].Identifier : (LseqIdentifier?)null;
                var newId = GenerateIdentifierBetween(lastId, null, replicaId);
                
                return new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Upsert, new LseqItem(newId, add.Value), context.Timestamp, context.Clock);
            }
            case InsertIntent insert:
            {
                if (insert.Index < 0 || insert.Index > lseqItems.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(insert.Index), "Index is out of range.");
                }

                var prevId = insert.Index > 0 ? lseqItems[insert.Index - 1].Identifier : (LseqIdentifier?)null;
                var nextId = insert.Index < lseqItems.Count ? lseqItems[insert.Index].Identifier : (LseqIdentifier?)null;
                var newId = GenerateIdentifierBetween(prevId, nextId, replicaId);
                
                return new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Upsert, new LseqItem(newId, insert.Value), context.Timestamp, context.Clock);
            }
            case RemoveIntent remove:
            {
                if (remove.Index < 0 || remove.Index >= lseqItems.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(remove.Index), "Index is out of range.");
                }

                return new CrdtOperation(Guid.NewGuid(), replicaId, context.JsonPath, OperationType.Remove, lseqItems[remove.Index].Identifier, context.Timestamp, context.Clock);
            }
            default:
                throw new NotSupportedException($"Explicit operation generation for intent '{context.Intent.GetType().Name}' is not supported in {nameof(LseqStrategy)}.");
        }
    }

    /// <inheritdoc />
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (!metadata.States.TryGetValue(operation.JsonPath, out var state) || state is not LseqState lseqState)
        {
            lseqState = new LseqState(new List<LseqItem>());
            metadata.States[operation.JsonPath] = lseqState;
        }
        var lseqItems = lseqState.Trackers;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (parent is null || property is null)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var list = property.Getter!(parent) as IList;

        if (list is null)
        {
            list = (IList)PocoPathHelper.InstantiateCollection(property.PropertyType, aotContexts);
            property.Setter!(parent, list);
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqItem), aotContexts) is LseqItem newItem)
                {
                    // O(1) Check for existing to ensure idempotency
                    var existingIdx = lseqItems.FindIndex(i => i.Identifier.Equals(newItem.Identifier));
                    if (existingIdx >= 0) return CrdtOperationStatus.Success;

                    // O(N) Find insertion position to maintain sorted order
                    int insertPos = 0;
                    while (insertPos < lseqItems.Count && lseqItems[insertPos].Identifier.CompareTo(newItem.Identifier) < 0)
                    {
                        insertPos++;
                    }

                    // Incrementally update both tracker and list without full reconstruction
                    lseqItems.Insert(insertPos, newItem);
                    list.Insert(insertPos, PocoPathHelper.ConvertValue(newItem.Value, elementType, aotContexts));
                }
                else
                {
                    return CrdtOperationStatus.StrategyApplicationFailed;
                }
                break;
            case OperationType.Remove:
                if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqIdentifier), aotContexts) is LseqIdentifier idToRemove)
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
                else
                {
                    return CrdtOperationStatus.StrategyApplicationFailed;
                }
                break;
            default:
                return CrdtOperationStatus.StrategyApplicationFailed;
        }

        metadata.States[operation.JsonPath] = lseqState;

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // LseqStrategy uses absolute positions and hard-deletes elements immediately.
        // There are no tombstones kept in metadata, so compaction is a no-op.
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
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
        else if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqItem), aotContexts) is LseqItem convItem) key = convItem.Identifier;
        else if (PocoPathHelper.ConvertValue(operation.Value, typeof(LseqIdentifier), aotContexts) is LseqIdentifier convId) key = convId;

        if (key is null) return null;
        if (key is IComparable comparableKey) return comparableKey;
    
        throw new InvalidOperationException($"The key of a partitionable Lseq must implement IComparable. Key: '{key}'");
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty)
    {
        return new LseqIdentifier(ImmutableList<LseqPathSegment>.Empty);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        if (!originalMetadata.States.TryGetValue(path, out var state) || state is not LseqState lseqState || lseqState.Trackers.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }
        var lseqItems = lseqState.Trackers;

        var splitIndex = lseqItems.Count / 2;
        var splitKey = lseqItems[splitIndex].Identifier;

        var items1 = lseqItems.Take(splitIndex).ToList();
        var items2 = lseqItems.Skip(splitIndex).ToList();

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts)!;

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        meta1.States[path] = new LseqState(items1);
        meta2.States[path] = new LseqState(items2);

        ReconstructListForSplitMerge(doc1, path, items1, aotContexts);
        ReconstructListForSplitMerge(doc2, path, items2, aotContexts);

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = PocoPathHelper.Instantiate(data1.GetType(), aotContexts)!;
        
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var items1 = meta1.States.TryGetValue(path, out var s1) && s1 is LseqState ls1 ? ls1.Trackers : new List<LseqItem>();
        var items2 = meta2.States.TryGetValue(path, out var s2) && s2 is LseqState ls2 ? ls2.Trackers : new List<LseqItem>();

        var mergedItemsDict = new Dictionary<LseqIdentifier, LseqItem>();
        foreach (var item in items1) mergedItemsDict[item.Identifier] = item;
        foreach (var item in items2) mergedItemsDict[item.Identifier] = item;

        var mergedItems = mergedItemsDict.Values.ToList();
        mergedItems.Sort((a, b) => a.Identifier.CompareTo(b.Identifier));

        mergedMeta.States[path] = new LseqState(mergedItems);

        ReconstructListForSplitMerge(mergedDoc, path, mergedItems, aotContexts);

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

    private static void ReconstructListForSplitMerge(object root, string path, List<LseqItem> lseqItems, IEnumerable<CrdtAotContext> aotContexts)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path, aotContexts);
        if (parent is null || property is null) return;

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var list = property.Getter!(parent) as IList;
        
        if (list is null)
        {
            list = (IList)PocoPathHelper.InstantiateCollection(property.PropertyType, aotContexts);
            property.Setter!(parent, list);
        }

        list.Clear();
        foreach (var item in lseqItems)
        {
            list.Add(PocoPathHelper.ConvertValue(item.Value, elementType, aotContexts));
        }
    }
}