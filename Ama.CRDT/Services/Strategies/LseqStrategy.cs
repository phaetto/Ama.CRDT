namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// Implements the LSEQ (Log-structured Sequence) strategy. LSEQ assigns dense, ordered identifiers
/// to list elements, which allows for generating a new identifier between any two existing ones.
/// This avoids floating-point precision issues while providing a stable, convergent order.
/// </summary>
public sealed class LseqStrategy : ICrdtStrategy
{
    private readonly IElementComparerProvider elementComparerProvider;
    private readonly string replicaId;
    private const int Base = 32;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LseqStrategy"/> class.
    /// </summary>
    public LseqStrategy(IElementComparerProvider elementComparerProvider, IOptions<CrdtOptions> options)
    {
        this.elementComparerProvider = elementComparerProvider ?? throw new ArgumentNullException(nameof(elementComparerProvider));
        ArgumentNullException.ThrowIfNull(options?.Value);
        this.replicaId = options.Value.ReplicaId;
    }

    /// <inheritdoc />
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        if (originalValue is not IList originalList || modifiedValue is not IList modifiedList) return;

        var elementType = property.PropertyType.IsGenericType
            ? property.PropertyType.GetGenericArguments()[0]
            : property.PropertyType.GetElementType() ?? typeof(object);
        var comparer = elementComparerProvider.GetComparer(elementType);

        if (!originalMeta.LseqTrackers.TryGetValue(path, out var originalItems)) return;
        
        var originalItemsByValue = originalItems.ToDictionary(i => i.Value, i => i, comparer);

        // Deletions
        foreach (var item in originalItems)
        {
            if (!modifiedList.Cast<object>().Contains(item.Value, comparer))
            {
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item.Identifier, new EpochTimestamp(0));
                operations.Add(op);
            }
        }

        // Insertions
        for (var i = 0; i < modifiedList.Count; i++)
        {
            var currentItem = modifiedList[i];
            if (!originalItemsByValue.ContainsKey(currentItem))
            {
                var prevItem = i > 0 ? modifiedList[i - 1] : null;
                var nextItem = i < originalList.Count ? originalList[i] : null;

                var prevId = prevItem != null && originalItemsByValue.TryGetValue(prevItem, out var p) ? p.Identifier : (LseqIdentifier?)null;
                var nextId = nextItem != null && originalItemsByValue.TryGetValue(nextItem, out var n) ? n.Identifier : (LseqIdentifier?)null;

                var newId = GenerateIdentifierBetween(prevId, nextId, replicaId);
                var newItem = new LseqItem(newId, currentItem);
                
                var op = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, new EpochTimestamp(0));
                operations.Add(op);
            }
        }
    }

    /// <inheritdoc />
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        if (!metadata.LseqTrackers.TryGetValue(operation.JsonPath, out var lseqItems))
        {
            lseqItems = new List<LseqItem>();
            metadata.LseqTrackers[operation.JsonPath] = lseqItems;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                var newItem = Deserialize<LseqItem>(operation.Value);
                if (!lseqItems.Any(i => i.Identifier.Equals(newItem.Identifier)))
                {
                    lseqItems.Add(newItem);
                }
                break;
            case OperationType.Remove:
                var idToRemove = Deserialize<LseqIdentifier>(operation.Value);
                lseqItems.RemoveAll(i => i.Identifier.Equals(idToRemove));
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
            var node1 = level < p1.Count ? p1[level] : (0, replicaId);
            var node2 = level < p2.Count ? p2[level] : (Base, replicaId);

            if (node1.Item1 > node2.Item1)
            {
                 // This shouldn't happen if identifiers are correctly ordered.
                 // As a fallback, use the previous path and append a new level.
                 newPath.AddRange(p1);
                 newPath.Add((Random.Shared.Next(1, Base), replicaId));
                 break;
            }

            var diff = node2.Item1 - node1.Item1;
            if (diff > 1)
            {
                var newPos = Random.Shared.Next(node1.Item1 + 1, node2.Item1);
                newPath.Add((newPos, replicaId));
                break;
            }
            
            // Positions are equal or adjacent, need to go deeper.
            newPath.Add(node1);
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
        
        var elementType = property.PropertyType.IsGenericType
            ? property.PropertyType.GetGenericArguments()[0]
            : property.PropertyType.GetElementType() ?? typeof(object);

        list.Clear();
        foreach (var item in lseqItems)
        {
            var value = item.Value is JsonElement je
                ? je.Deserialize(elementType)
                : item.Value;
            list.Add(value);
        }
    }
    
    private static T? Deserialize<T>(object? value)
    {
        if (value is JsonElement je)
        {
            return je.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        return (T?)value;
    }
}