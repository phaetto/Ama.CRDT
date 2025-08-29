namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Services;

/// <summary>
/// Implements the 2P-Set (Two-Phase Set) CRDT strategy.
/// In a 2P-Set, an element can be added and removed, but once removed, it cannot be re-added.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class TwoPhaseSetStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalSet = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedSet = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        
        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var comparer = comparerProvider.GetComparer(elementType);

        var added = modifiedSet.Except(originalSet, comparer);
        var removed = originalSet.Except(modifiedSet, comparer);

        foreach (var item in added)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, changeTimestamp));
        }

        foreach (var item in removed)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item, changeTimestamp));
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IList list) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var comparer = comparerProvider.GetComparer(elementType);

        if (!metadata.TwoPhaseSets.TryGetValue(operation.JsonPath, out var state))
        {
            state = new TwoPhaseSetState(new HashSet<object>(comparer), new HashSet<object>(comparer));
            metadata.TwoPhaseSets[operation.JsonPath] = state;
        }

        var itemValue = PocoPathHelper.ConvertValue(operation.Value, elementType);
        if (itemValue is null) return;

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (!state.Tomstones.Contains(itemValue))
                {
                    state.Adds.Add(itemValue);
                }
                break;
            case OperationType.Remove:
                state.Tomstones.Add(itemValue);
                break;
        }

        ReconstructList(list, state.Adds, state.Tomstones);
    }
    
    private static void ReconstructList(IList list, ISet<object> adds, ISet<object> tombstones)
    {
        var liveItems = adds.Except(tombstones);
        
        // Sort the items to ensure a deterministic order across all replicas.
        var sortedItems = liveItems.OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
        
        list.Clear();
        foreach (var item in sortedItems)
        {
            list.Add(item);
        }
    }
}