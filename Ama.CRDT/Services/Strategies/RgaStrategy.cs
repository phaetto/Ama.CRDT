namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
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
using System.Reflection;

/// <summary>
/// Implements the RGA (Replicated Growable Array) strategy.
/// RGA maintains order by linking elements to their predecessors (causal trees) and uses tombstones for deletions.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(InsertIntent))]
[CrdtSupportedIntent(typeof(RemoveIntent))]
[Commutative]
[Associative]
[Idempotent]
[OperationBased]
public sealed class RgaStrategy(
    IElementComparerProvider elementComparerProvider,
    ICrdtTimestampProvider timestampProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc />
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        return GetMinimumKey(partitionableProperty);
    }

    /// <inheritdoc />
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (operation.JsonPath != partitionablePropertyPath) return null;

        if (operation.Type == OperationType.Upsert && PocoPathHelper.ConvertValue(operation.Value, typeof(RgaItem)) is RgaItem item)
        {
            return item.Identifier;
        }
        if (operation.Type == OperationType.Remove && PocoPathHelper.ConvertValue(operation.Value, typeof(RgaIdentifier)) is RgaIdentifier id)
        {
            return id;
        }

        return null;
    }

    /// <inheritdoc />
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        return new RgaIdentifier(0, string.Empty);
    }

    /// <inheritdoc />
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";
        
        if (!originalMetadata.RgaTrackers.TryGetValue(path, out var items))
        {
            items = new List<RgaItem>();
        }

        var sortedItems = items.OrderBy(x => x.Identifier).ToList();

        var splitIndex = sortedItems.Count / 2;
        var splitKey = sortedItems.Count > 0 && splitIndex < sortedItems.Count 
            ? sortedItems[splitIndex].Identifier 
            : GetMinimumKey(partitionableProperty);

        var leftItems = sortedItems.Take(splitIndex).ToList();
        var rightItems = sortedItems.Skip(splitIndex).ToList();

        var leftMeta = originalMetadata.DeepClone();
        var rightMeta = originalMetadata.DeepClone();
        
        leftMeta.RgaTrackers[path] = leftItems;
        rightMeta.RgaTrackers[path] = rightItems;

        var leftData = Activator.CreateInstance(originalData.GetType())!;
        var rightData = Activator.CreateInstance(originalData.GetType())!;

        ReconstructList(leftData, path, RebuildRgaOrder(leftItems));
        ReconstructList(rightData, path, RebuildRgaOrder(rightItems));

        return new SplitResult(
            new PartitionContent(leftData, leftMeta),
            new PartitionContent(rightData, rightMeta),
            splitKey
        );
    }

    /// <inheritdoc />
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        meta1.RgaTrackers.TryGetValue(path, out var items1);
        meta2.RgaTrackers.TryGetValue(path, out var items2);

        items1 ??= new List<RgaItem>();
        items2 ??= new List<RgaItem>();

        var mergedItems = items1.Concat(items2).DistinctBy(x => x.Identifier).ToList();
        mergedItems = RebuildRgaOrder(mergedItems);

        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);
        mergedMeta.RgaTrackers[path] = mergedItems;

        var mergedData = Activator.CreateInstance(data1.GetType())!;
        ReconstructList(mergedData, path, mergedItems);

        return new PartitionContent(mergedData, mergedMeta);
    }

    /// <inheritdoc />
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        if (originalValue is not IList originalList || modifiedValue is not IList modifiedList) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var comparer = elementComparerProvider.GetComparer(elementType);

        if (!originalMeta.RgaTrackers.TryGetValue(path, out var originalItems))
        {
            originalItems = new List<RgaItem>();
        }

        var originalVisible = originalItems.Where(x => !x.IsDeleted).ToList();

        // 1. O(N) Optimization: Trim common prefix
        int start = 0;
        while (start < originalVisible.Count && start < modifiedList.Count &&
               comparer!.Equals(originalVisible[start].Value, modifiedList[start]))
        {
            start++;
        }

        // 2. O(N) Optimization: Trim common suffix
        int endOrig = originalVisible.Count - 1;
        int endMod = modifiedList.Count - 1;
        while (endOrig >= start && endMod >= start &&
               comparer!.Equals(originalVisible[endOrig].Value, modifiedList[endMod]))
        {
            endOrig--;
            endMod--;
        }

        // Slice the arrays to only compute LCS on the changed middle section
        var slicedOrig = new List<RgaItem>(endOrig - start + 1);
        for (int i = start; i <= endOrig; i++) slicedOrig.Add(originalVisible[i]);

        var slicedMod = new List<object?>(endMod - start + 1);
        for (int i = start; i <= endMod; i++) slicedMod.Add(modifiedList[i]);

        // Calculate LCS on the much smaller trimmed slices
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
        // If we have a prefix, the predecessor for the first insertion is the last element of the prefix.
        RgaIdentifier? lastId = start > 0 ? originalVisible[start - 1].Identifier : null;
        var origIdx = 0;
        var ticksBase = DateTime.UtcNow.Ticks;

        for (var i = 0; i < slicedMod.Count; i++)
        {
            if (matchedModified[i])
            {
                while (!matchedOriginal[origIdx]) origIdx++;
                lastId = slicedOrig[origIdx].Identifier;
                origIdx++;
            }
            else
            {
                var newId = new RgaIdentifier(ticksBase + i, replicaId);
                var newItem = new RgaItem(newId, lastId, slicedMod[i], false);
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, changeTimestamp, clock);
                operations.Add(op);
                lastId = newId;
            }
        }
    }

    /// <inheritdoc />
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (_, metadata, path, _, intent, timestamp, clock) = context;

        if (!metadata.RgaTrackers.TryGetValue(path, out var items))
        {
            items = new List<RgaItem>();
        }

        var visibleItems = items.Where(x => !x.IsDeleted).ToList();

        if (intent is AddIntent addIntent)
        {
            RgaIdentifier? leftId = visibleItems.Count > 0 ? visibleItems[^1].Identifier : null;
            var newId = new RgaIdentifier(DateTime.UtcNow.Ticks, replicaId);
            var newItem = new RgaItem(newId, leftId, addIntent.Value, false);

            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, timestamp, clock);
        }

        if (intent is InsertIntent insertIntent)
        {
            if (insertIntent.Index < 0 || insertIntent.Index > visibleItems.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(intent), "Index is out of range.");
            }

            RgaIdentifier? leftId = insertIntent.Index > 0 ? visibleItems[insertIntent.Index - 1].Identifier : null;
            var newId = new RgaIdentifier(DateTime.UtcNow.Ticks, replicaId);
            var newItem = new RgaItem(newId, leftId, insertIntent.Value, false);

            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, timestamp, clock);
        }
        
        if (intent is RemoveIntent removeIntent)
        {
            if (removeIntent.Index < 0 || removeIntent.Index >= visibleItems.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(intent), "Index is out of range.");
            }

            var idToRemove = visibleItems[removeIntent.Index].Identifier;
            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, idToRemove, timestamp, clock);
        }
        
        throw new NotSupportedException($"Intent type '{intent.GetType().Name}' is not supported by {nameof(RgaStrategy)}.");
    }

    /// <inheritdoc />
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (!metadata.RgaTrackers.TryGetValue(operation.JsonPath, out var items))
        {
            items = new List<RgaItem>();
            metadata.RgaTrackers[operation.JsonPath] = items;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var list = PocoPathHelper.GetAccessor(property).Getter(parent) as IList;

        if (list is null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            list = (IList)Activator.CreateInstance(listType)!;
            PocoPathHelper.GetAccessor(property).Setter(parent, list);
        }

        if (operation.Type == OperationType.Upsert)
        {
            if (PocoPathHelper.ConvertValue(operation.Value, typeof(RgaItem)) is RgaItem newItem)
            {
                if (items.Any(x => x.Identifier.Equals(newItem.Identifier)))
                {
                    return CrdtOperationStatus.Success; // Idempotency check
                }

                // O(1) Fast path: Appending to the very end of the list (e.g., standard typing)
                if (items.Count == 0 || (newItem.LeftIdentifier.HasValue && items[^1].Identifier.Equals(newItem.LeftIdentifier.Value)))
                {
                    items.Add(newItem);
                    list.Add(PocoPathHelper.ConvertValue(newItem.Value, elementType));
                }
                else
                {
                    // General path for concurrent inserts / edits in the middle of the text
                    items.Add(newItem);
                    items = RebuildRgaOrder(items);

                    int visibleIdx = 0;
                    foreach (var item in items)
                    {
                        if (item.Identifier.Equals(newItem.Identifier)) break;
                        if (!item.IsDeleted) visibleIdx++;
                    }
                    
                    list.Insert(visibleIdx, PocoPathHelper.ConvertValue(newItem.Value, elementType));
                }
            }
            else
            {
                return CrdtOperationStatus.StrategyApplicationFailed;
            }
        }
        else if (operation.Type == OperationType.Remove)
        {
            if (PocoPathHelper.ConvertValue(operation.Value, typeof(RgaIdentifier)) is RgaIdentifier idToRemove)
            {
                int visibleIdx = 0;
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i].Identifier.Equals(idToRemove))
                    {
                        if (!items[i].IsDeleted)
                        {
                            items[i] = items[i] with { 
                                IsDeleted = true, 
                                DeletedByReplicaId = operation.ReplicaId, 
                                DeletedAtClock = operation.Clock 
                            };
                            if (visibleIdx < list.Count)
                            {
                                list.RemoveAt(visibleIdx);
                            }
                        }
                        break;
                    }
                    if (!items[i].IsDeleted) visibleIdx++;
                }
            }
            else
            {
                return CrdtOperationStatus.StrategyApplicationFailed;
            }
        }
        else
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        metadata.RgaTrackers[operation.JsonPath] = items;

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        if (!context.Metadata.RgaTrackers.TryGetValue(context.PropertyPath, out var items))
        {
            return;
        }

        var itemLookup = new Dictionary<RgaIdentifier, RgaItem>();
        var referenceCounts = new Dictionary<RgaIdentifier, int>();

        foreach (var item in items)
        {
            itemLookup[item.Identifier] = item;
            if (item.LeftIdentifier.HasValue)
            {
                referenceCounts.TryGetValue(item.LeftIdentifier.Value, out var count);
                referenceCounts[item.LeftIdentifier.Value] = count + 1;
            }
        }

        var itemsToRemove = new HashSet<RgaIdentifier>();
        var candidates = new Queue<RgaItem>();

        foreach (var item in items)
        {
            if (item.IsDeleted)
            {
                referenceCounts.TryGetValue(item.Identifier, out var count);
                if (count == 0)
                {
                    candidates.Enqueue(item);
                }
            }
        }

        while (candidates.Count > 0)
        {
            var item = candidates.Dequeue();
            
            // RGA items don't store insertion timestamps via ICrdtTimestamp natively, so we approximate with the ticks from the identifier.
            long unixMs = (item.Identifier.Timestamp - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
            if (context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: new EpochTimestamp(unixMs), ReplicaId: item.DeletedByReplicaId, Version: item.DeletedAtClock)))
            {
                itemsToRemove.Add(item.Identifier);
                
                if (item.LeftIdentifier.HasValue)
                {
                    var parentId = item.LeftIdentifier.Value;
                    if (referenceCounts.TryGetValue(parentId, out var count))
                    {
                        referenceCounts[parentId] = count - 1;
                        
                        if (referenceCounts[parentId] == 0 && 
                            itemLookup.TryGetValue(parentId, out var parentItem) && 
                            parentItem.IsDeleted)
                        {
                            candidates.Enqueue(parentItem);
                        }
                    }
                }
            }
        }

        if (itemsToRemove.Count > 0)
        {
            if (items is List<RgaItem> list)
            {
                list.RemoveAll(x => itemsToRemove.Contains(x.Identifier));
            }
            else
            {
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (itemsToRemove.Contains(items[i].Identifier))
                    {
                        items.RemoveAt(i);
                    }
                }
            }
        }
    }

    private static List<RgaItem> RebuildRgaOrder(List<RgaItem> items)
    {
        var childrenMap = new Dictionary<RgaIdentifier, List<RgaItem>>();
        var roots = new List<RgaItem>();

        var itemIds = new HashSet<RgaIdentifier>(items.Select(i => i.Identifier));

        foreach (var item in items)
        {
            if (item.LeftIdentifier.HasValue && itemIds.Contains(item.LeftIdentifier.Value))
            {
                if (!childrenMap.TryGetValue(item.LeftIdentifier.Value, out var children))
                {
                    children = new List<RgaItem>();
                    childrenMap[item.LeftIdentifier.Value] = children;
                }
                children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        roots.Sort((a, b) => b.Identifier.CompareTo(a.Identifier));

        var result = new List<RgaItem>(items.Count);

        void Traverse(RgaItem node)
        {
            result.Add(node);
            if (childrenMap.TryGetValue(node.Identifier, out var children))
            {
                children.Sort((a, b) => b.Identifier.CompareTo(a.Identifier));
                foreach (var child in children)
                {
                    Traverse(child);
                }
            }
        }

        foreach (var root in roots)
        {
            Traverse(root);
        }

        return result;
    }

    private static void ReconstructList(object root, string path, List<RgaItem> rgaItems)
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
        foreach (var item in rgaItems)
        {
            if (!item.IsDeleted)
            {
                list.Add(PocoPathHelper.ConvertValue(item.Value, elementType));
            }
        }
    }
}