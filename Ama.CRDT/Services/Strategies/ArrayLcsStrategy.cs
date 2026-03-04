namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System.Collections;
using System.Globalization;
using System.Reflection;

/// <inheritdoc/>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(InsertIntent))]
[CrdtSupportedIntent(typeof(RemoveIntent))]
[Commutative]
[Associative]
[Idempotent]
[OperationBased]
public sealed class ArrayLcsStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        var originalList = (originalValue as IList)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedList = (modifiedValue as IList)?.Cast<object>().ToList() ?? new List<object>();

        if (originalList.SequenceEqual(modifiedList))
        {
            return;
        }

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        
        var comparer = comparerProvider.GetComparer(elementType);

        var originalPositions = GetOrInitializePositions(originalList, originalMeta, path);
        
        var lcs = LongestCommonSubsequence(originalList, modifiedList, comparer);
        
        var lcsOriginalIndices = lcs.Select(t => t.Item1).ToHashSet();
        for (var i = 0; i < originalList.Count; i++)
        {
            if (!lcsOriginalIndices.Contains(i))
            {
                var removedIdentifier = originalPositions[i];
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, removedIdentifier, changeTimestamp, clock));
            }
        }

        var lcsWithBoundaries = new List<(int, int)> { (-1, -1) };
        lcsWithBoundaries.AddRange(lcs);
        lcsWithBoundaries.Add((originalList.Count, modifiedList.Count));

        for (var i = 0; i < lcsWithBoundaries.Count - 1; i++)
        {
            var start = lcsWithBoundaries[i];
            var end = lcsWithBoundaries[i+1];

            PositionalIdentifier? beforePos = start.Item1 >= 0 ? originalPositions[start.Item1] : null;
            PositionalIdentifier? afterPos = end.Item1 < originalList.Count ? originalPositions[end.Item1] : null;

            var lastGeneratedPos = beforePos?.Position;

            for (var modIdx = start.Item2 + 1; modIdx < end.Item2; modIdx++)
            {
                var newPos = GeneratePositionBetween(lastGeneratedPos, afterPos?.Position);
                var newItem = new PositionalItem(newPos, modifiedList[modIdx]);
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, changeTimestamp, clock));
                lastGeneratedPos = newPos;
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var root = context.DocumentRoot;
        var metadata = context.Metadata;
        var path = context.JsonPath;
        var intent = context.Intent;
        var timestamp = context.Timestamp;

        var (parent, resolvedProperty, _) = PocoPathHelper.ResolvePath(root, path);
        var list = (parent != null && resolvedProperty != null ? PocoPathHelper.GetAccessor(resolvedProperty).Getter(parent) as IList : null) ?? new List<object>();

        var positions = GetOrInitializePositions(list, metadata, path);

        if (intent is AddIntent addIntent)
        {
            PositionalIdentifier? beforePos = positions.Count > 0 ? positions[^1] : null;
            var newPos = GeneratePositionBetween(beforePos?.Position, null);
            var newItem = new PositionalItem(newPos, addIntent.Value);

            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, timestamp, context.Clock);
        }

        if (intent is InsertIntent insertIntent)
        {
            if (insertIntent.Index < 0 || insertIntent.Index > list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(context), "Insert index is out of range.");
            }

            PositionalIdentifier? beforePos = insertIntent.Index > 0 ? positions[insertIntent.Index - 1] : null;
            PositionalIdentifier? afterPos = insertIntent.Index < positions.Count ? positions[insertIntent.Index] : null;

            var newPos = GeneratePositionBetween(beforePos?.Position, afterPos?.Position);
            var newItem = new PositionalItem(newPos, insertIntent.Value);

            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, timestamp, context.Clock);
        }

        if (intent is RemoveIntent removeIntent)
        {
            if (removeIntent.Index < 0 || removeIntent.Index >= list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(context), "Remove index is out of range.");
            }

            var removedIdentifier = positions[removeIntent.Index];
            return new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, removedIdentifier, timestamp, context.Clock);
        }

        throw new NotSupportedException($"Intent {intent.GetType().Name} is not supported by {nameof(ArrayLcsStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IList list) return;

        if (!metadata.PositionalTrackers.TryGetValue(operation.JsonPath, out var positions))
        {
            positions = new List<PositionalIdentifier>();
            metadata.PositionalTrackers[operation.JsonPath] = positions;
        }

        if (list.Count > 0 && positions.Count == 0)
        {
            for (var i = 0; i < list.Count; i++)
            {
                positions.Add(new PositionalIdentifier((i + 1).ToString(CultureInfo.InvariantCulture), Guid.Empty));
            }
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                ApplyUpsert(list, positions, operation, property);
                break;
            case OperationType.Remove:
                ApplyRemove(list, positions, operation);
                break;
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var list = (IList?)PocoPathHelper.GetAccessor(partitionableProperty).Getter(data);
        if (list is null || list.Count == 0) return null;
        
        return new PositionalIdentifier("1", Guid.Empty);
    }
    
    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal))
        {
            return null;
        }

        if (operation.Value is PositionalItem item) return new PositionalIdentifier(item.Position, operation.Id);
        if (operation.Value is PositionalIdentifier identifier) return identifier;

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(PositionalItem)) is PositionalItem convertedItem)
        {
            return new PositionalIdentifier(convertedItem.Position, operation.Id);
        }
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(PositionalIdentifier)) is PositionalIdentifier convertedIdentifier)
        {
            return convertedIdentifier;
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        // For ArrayLcsStrategy, the key type is always PositionalIdentifier, which is internal to the strategy.
        // The provided property is not used because the key is strategy-defined.
        // "0" is chosen as it is guaranteed to be less than any position generated for actual elements (which start at "1").
        return new PositionalIdentifier("0", Guid.Empty);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var list = (IList)PocoPathHelper.GetAccessor(partitionableProperty).Getter(originalData)!;
        if (list.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        if (!originalMetadata.PositionalTrackers.TryGetValue(path, out var positions) || positions.Count != list.Count)
        {
            throw new InvalidOperationException("Positional metadata is out of sync, cannot split.");
        }

        var splitIndex = list.Count / 2;
        var splitKey = positions[splitIndex];

        var doc1 = Activator.CreateInstance(documentType)!;
        var list1 = (IList)Activator.CreateInstance(list.GetType())!;
        for (int i = 0; i < splitIndex; i++) list1.Add(list[i]);
        PocoPathHelper.GetAccessor(partitionableProperty).Setter(doc1, list1);
        
        var meta1 = originalMetadata.DeepClone();
        meta1.PositionalTrackers[path] = positions.Take(splitIndex).ToList();

        var doc2 = Activator.CreateInstance(documentType)!;
        var list2 = (IList)Activator.CreateInstance(list.GetType())!;
        for (int i = splitIndex; i < list.Count; i++) list2.Add(list[i]);
        PocoPathHelper.GetAccessor(partitionableProperty).Setter(doc2, list2);
        
        var meta2 = originalMetadata.DeepClone();
        meta2.PositionalTrackers[path] = positions.Skip(splitIndex).ToList();

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var list1 = (IList)PocoPathHelper.GetAccessor(partitionableProperty).Getter(data1)!;
        var list2 = (IList)PocoPathHelper.GetAccessor(partitionableProperty).Getter(data2)!;

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var mergedList = (IList)Activator.CreateInstance(list1.GetType())!;

        foreach (var item in list1) mergedList.Add(item);
        foreach (var item in list2) mergedList.Add(item);
        
        PocoPathHelper.GetAccessor(partitionableProperty).Setter(mergedDoc, mergedList);
        
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);
        
        var positions1 = meta1.PositionalTrackers.TryGetValue(path, out var p1) ? p1 : new List<PositionalIdentifier>();
        var positions2 = meta2.PositionalTrackers.TryGetValue(path, out var p2) ? p2 : new List<PositionalIdentifier>();
        
        mergedMeta.PositionalTrackers[path] = positions1.Concat(positions2).OrderBy(p => p).ToList();

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private void ApplyUpsert(IList list, List<PositionalIdentifier> positions, CrdtOperation operation, PropertyInfo collectionProperty)
    {
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(PositionalItem)) is not PositionalItem item) return;

        var elementType = PocoPathHelper.GetCollectionElementType(collectionProperty);
        var itemValue = PocoPathHelper.ConvertValue(item.Value, elementType);

        var newIdentifier = new PositionalIdentifier(item.Position, operation.Id);

        var index = positions.BinarySearch(newIdentifier);
        if (index >= 0)
        {
            // Exact position already exists. This can happen with concurrent insertions.
            // The CRDT relies on operation IDs to create a total order.
            var existingIdentifier = positions[index];
            if (newIdentifier.OperationId.CompareTo(existingIdentifier.OperationId) > 0)
            {
                // Our op ID is greater, insert after.
                index++;
            }
        }
        else
        {
            index = ~index;
        }
        
        positions.Insert(index, newIdentifier);
        list.Insert(index, itemValue);
    }
    
    private void ApplyRemove(IList list, List<PositionalIdentifier> positions, CrdtOperation operation)
    {
        if (PocoPathHelper.ConvertValue(operation.Value, typeof(PositionalIdentifier)) is not PositionalIdentifier identifier) return;
        
        var index = positions.IndexOf(identifier);

        if (index >= 0)
        {
            positions.RemoveAt(index);
            list.RemoveAt(index);
        }
    }

    private static List<PositionalIdentifier> GetOrInitializePositions(IList currentList, CrdtMetadata metadata, string path)
    {
        if (!metadata.PositionalTrackers.TryGetValue(path, out var positions)
            || positions.Count != currentList.Count
            || positions.All(p => p.OperationId == Guid.Empty))
        {
            var newPositions = new List<PositionalIdentifier>();
            for (var i = 0; i < currentList.Count; i++)
            {
                newPositions.Add(new PositionalIdentifier((i + 1).ToString(CultureInfo.InvariantCulture), Guid.Empty));
            }
            return newPositions;
        }
        return positions;
    }
    
    private static string GeneratePositionBetween(string? posBefore, string? posAfter)
    {
        var decBefore = posBefore != null ? decimal.Parse(posBefore, CultureInfo.InvariantCulture) : 0m;

        if (posAfter is null)
        {
            return (decBefore + 1m).ToString("G29", CultureInfo.InvariantCulture);
        }
    
        var decAfter = decimal.Parse(posAfter, CultureInfo.InvariantCulture);

        if (decAfter <= decBefore)
        {
            // This handles equality and out-of-order cases.
            // When equal, we must return the same position, relying on OperationId for tie-breaking.
            return posBefore!;
        }

        return ((decBefore + decAfter) / 2m).ToString("G29", CultureInfo.InvariantCulture);
    }

    private static List<(int, int)> LongestCommonSubsequence(List<object> seq1, List<object> seq2, IEqualityComparer<object> comparer)
    {
        var lengths = new int[seq1.Count + 1, seq2.Count + 1];
        for (var i = 0; i < seq1.Count; i++)
        {
            for (var j = 0; j < seq2.Count; j++)
            {
                if (comparer.Equals(seq1[i], seq2[j]))
                {
                    lengths[i + 1, j + 1] = lengths[i, j] + 1;
                }
                else
                {
                    lengths[i + 1, j + 1] = Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }
        }

        var lcs = new List<(int, int)>();
        int x = seq1.Count, y = seq2.Count;
        while (x > 0 && y > 0)
        {
            if (comparer.Equals(seq1[x - 1], seq2[y - 1]))
            {
                lcs.Add((x - 1, y - 1));
                x--;
                y--;
            }
            else if (lengths[x - 1, y] > lengths[x, y - 1])
            {
                x--;
            }
            else
            {
                y--;
            }
        }
        lcs.Reverse();
        return lcs;
    }
}