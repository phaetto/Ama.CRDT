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
/// Implements the OR-Set (Observed-Remove Set) CRDT strategy.
/// This set allows re-addition of elements by assigning a unique tag to each added instance.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class OrSetStrategy(
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
            var payload = new OrSetAddItem(item, Guid.NewGuid());
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp));
        }

        if (originalMeta.OrSets.TryGetValue(path, out var metaState))
        {
            foreach (var item in removed)
            {
                if (metaState.Adds.TryGetValue(item, out var tags) && tags.Count > 0)
                {
                    var payload = new OrSetRemoveItem(item, new HashSet<Guid>(tags));
                    operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, payload, changeTimestamp));
                }
            }
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

        if (!metadata.OrSets.TryGetValue(operation.JsonPath, out var state))
        {
            state = (new Dictionary<object, ISet<Guid>>(comparer), new Dictionary<object, ISet<Guid>>(comparer));
            metadata.OrSets[operation.JsonPath] = state;
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                ApplyUpsert(state, operation.Value, elementType);
                break;
            case OperationType.Remove:
                ApplyRemove(state, operation.Value, elementType);
                break;
        }

        ReconstructList(list, state.Adds, state.Removes, comparer);
    }
    
    private static void ApplyUpsert((IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, object? opValue, Type elementType)
    {
        if (PocoPathHelper.ConvertValue(opValue, typeof(OrSetAddItem)) is not OrSetAddItem payload) return;
        
        var itemValue = PocoPathHelper.ConvertValue(payload.Value, elementType);
        if (itemValue is null) return;
        
        if (!state.Adds.TryGetValue(itemValue, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[itemValue] = addTags;
        }
        addTags.Add(payload.Tag);
    }

    private static void ApplyRemove((IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, object? opValue, Type elementType)
    {
        if (PocoPathHelper.ConvertValue(opValue, typeof(OrSetRemoveItem)) is not OrSetRemoveItem payload) return;

        var itemValue = PocoPathHelper.ConvertValue(payload.Value, elementType);
        if (itemValue is null) return;

        if (!state.Removes.TryGetValue(itemValue, out var removeTags))
        {
            removeTags = new HashSet<Guid>();
            state.Removes[itemValue] = removeTags;
        }
        foreach (var tag in payload.Tags)
        {
            removeTags.Add(tag);
        }
    }
    
    private static void ReconstructList(IList list, IDictionary<object, ISet<Guid>> adds, IDictionary<object, ISet<Guid>> removes, IEqualityComparer<object> comparer)
    {
        var liveItems = new HashSet<object>(comparer);

        foreach (var (item, addTags) in adds)
        {
            if (removes.TryGetValue(item, out var removeTags))
            {
                if (addTags.Except(removeTags).Any())
                {
                    liveItems.Add(item);
                }
            }
            else
            {
                liveItems.Add(item);
            }
        }
        
        // Sort the items to ensure a deterministic order across all replicas.
        var sortedItems = liveItems.OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
        
        list.Clear();
        foreach (var item in sortedItems)
        {
            list.Add(item);
        }
    }
}