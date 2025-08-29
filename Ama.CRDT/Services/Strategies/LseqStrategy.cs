namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System.Collections;
using System.Collections.Immutable;
using Ama.CRDT.Services;

/// <summary>
/// Implements the LSEQ (Log-structured Sequence) strategy. LSEQ assigns dense, ordered identifiers
/// to list elements, which allows for generating a new identifier between any two existing ones.
/// This avoids floating-point precision issues while providing a stable, convergent order.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[Commutative]
[Associative]
[Idempotent]
[SequentialOperations]
public sealed class LseqStrategy(
    IElementComparerProvider elementComparerProvider,
    ICrdtTimestampProvider timestampProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
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
        var sortedOriginalItems = originalItems.OrderBy(i => i.Identifier).ToList();
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

            // Find the identifier of the next element in the original sequence.
            LseqIdentifier? nextId = null;
            if (prevId != null)
            {
                var prevIndex = sortedOriginalItems.FindIndex(item => item.Identifier.Equals(prevId));
                if (prevIndex > -1 && prevIndex + 1 < sortedOriginalItems.Count)
                {
                    nextId = sortedOriginalItems[prevIndex + 1].Identifier;
                }
            }
            else // This is an insert at the beginning of the list
            {
                if (sortedOriginalItems.Any())
                {
                    nextId = sortedOriginalItems[0].Identifier;
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

    private LseqIdentifier GenerateIdentifierBetween(LseqIdentifier? prev, LseqIdentifier? next, string replicaId)
    {
        var p1 = prev?.Path ?? ImmutableList<(int, string)>.Empty;
        var p2 = next?.Path ?? ImmutableList<(int, string)>.Empty;

        var newPath = ImmutableList.CreateBuilder<(int, string)>();
        var level = 0;

        while (true)
        {
            var head1 = level < p1.Count ? p1[level] : (0, string.Empty);
            var head2 = level < p2.Count ? p2[level] : (Base, string.Empty);

            var diff = head2.Item1 - head1.Item1;
            if (diff > 1)
            {
                var newPos = head1.Item1 + 1;
                newPath.Add((newPos, replicaId));
                break;
            }

            var prefixNode = level < p1.Count ? p1[level] : head1;
            newPath.Add(prefixNode);
            level++;
        }

        return new LseqIdentifier(newPath.ToImmutable());
    }

    private static void ReconstructList(object root, string path, List<LseqItem> lseqItems)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        if (parent is null || property is null) return;

        var list = property.GetValue(parent) as IList;
        if (list is null) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);

        list.Clear();
        foreach (var item in lseqItems)
        {
            list.Add(PocoPathHelper.ConvertValue(item.Value, elementType));
        }
    }
}