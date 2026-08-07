namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <inheritdoc/>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(AddIntent))]
[CrdtSupportedIntent(typeof(RemoveValueIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class PriorityQueueStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        var originalList = originalValue as IEnumerable;
        var modifiedList = modifiedValue as IEnumerable;

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);

        if (property.StrategyAttribute is not CrdtPriorityQueueStrategyAttribute attr) return;
        var priorityPropertyName = attr.PriorityPropertyName ?? "Priority";

        var comparer = comparerProvider.GetComparer(elementType);

        var originalDict = originalList?.Cast<object>().ToDictionary(item => item, item => item, comparer) ?? new Dictionary<object, object>(comparer);
        var modifiedDict = modifiedList?.Cast<object>().ToDictionary(item => item, item => item, comparer) ?? new Dictionary<object, object>(comparer);

        var originalMetaState = GetOrUpdateState(originalMeta, path, comparer);
        var originalAdds = originalMetaState.Adds;
        var originalRemoves = originalMetaState.Removes;

        // Find removals
        foreach (var item in originalDict.Values.Where(o => !modifiedDict.ContainsKey(o)))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item, changeTimestamp, clock));
        }

        // Find additions and updates
        foreach (var modifiedItem in modifiedDict.Values)
        {
            var isNew = !originalDict.TryGetValue(modifiedItem, out var originalItem);

            var hasChanged = false;
            if (!isNew && originalItem is not null)
            {
                // Use reflection accessor to compare the priority property's value.
                // This detects changes in priority for an existing item, which is necessary
                // when the equality comparer for the item type only considers identity.
                var originalPriority = PocoPathHelper.GetValue(originalItem, priorityPropertyName, aotContexts);
                var modifiedPriority = PocoPathHelper.GetValue(modifiedItem, priorityPropertyName, aotContexts);

                if (!Equals(originalPriority, modifiedPriority))
                {
                    hasChanged = true;
                }
            }

            if (isNew || hasChanged)
            {
                if (originalRemoves.TryGetValue(modifiedItem, out var removeCausalTs) && changeTimestamp.CompareTo(removeCausalTs.Timestamp) < 0)
                {
                    continue;
                }

                if (!isNew && originalItem is not null && originalAdds.TryGetValue(originalItem, out var addTimestamp) && changeTimestamp.CompareTo(addTimestamp) < 0)
                {
                    continue;
                }

                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedItem, changeTimestamp, clock));
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (_, _, jsonPath, property, intent, timestamp, clock) = context;

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);

        return intent switch
        {
            AddIntent addIntent => new CrdtOperation(Guid.NewGuid(), replicaId, jsonPath, OperationType.Upsert, PocoPathHelper.ConvertValue(addIntent.Value, elementType, aotContexts), timestamp, clock),
            RemoveValueIntent removeIntent => new CrdtOperation(Guid.NewGuid(), replicaId, jsonPath, OperationType.Remove, PocoPathHelper.ConvertValue(removeIntent.Value, elementType, aotContexts), timestamp, clock),
            _ => throw new NotSupportedException($"Intent {intent.GetType().Name} is not supported for {nameof(PriorityQueueStrategy)}.")
        };
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (parent is null || property is null || property.Getter!(parent) is not IList list)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }
        
        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        
        var comparer = comparerProvider.GetComparer(elementType);
        
        var meta = GetOrUpdateState(metadata, operation.JsonPath, comparer);
        var adds = meta.Adds;
        var removes = meta.Removes;

        var value = PocoPathHelper.ConvertValue(operation.Value, elementType, aotContexts);
        if (value is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }
        
        bool wasApplied = false;

        switch (operation.Type)
        {
            case OperationType.Upsert:
                wasApplied = ApplyUpsert(list, adds, removes, value, operation, comparer);
                break;
            case OperationType.Remove:
                wasApplied = ApplyRemove(list, adds, removes, value, operation, comparer);
                break;
            default:
                return CrdtOperationStatus.StrategyApplicationFailed;
        }
        
        if (wasApplied)
        {
            SortList(list, property);
            return CrdtOperationStatus.Success;
        }

        return CrdtOperationStatus.Obsolete;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        if (!context.Metadata.States.TryGetValue(context.PropertyPath, out var baseState) || baseState is not LwwSetState state) return;

        var deadItemsToRemove = new List<object>();

        foreach (var kvp in state.Removes)
        {
            var item = kvp.Key;
            var removeCausalTs = kvp.Value;

            if (state.Adds.TryGetValue(item, out var addTs))
            {
                // If addTs <= removeTs, the Remove operation is newer (or equal).
                // Thus, the item is dead.
                if (addTs.CompareTo(removeCausalTs.Timestamp) <= 0)
                {
                    if (context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: removeCausalTs.Timestamp, ReplicaId: removeCausalTs.ReplicaId, Version: removeCausalTs.Clock)) && context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: addTs)))
                    {
                        deadItemsToRemove.Add(item);
                    }
                }
            }
            else
            {
                // Item is in Removes but not in Adds, so it's dead.
                if (context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: removeCausalTs.Timestamp, ReplicaId: removeCausalTs.ReplicaId, Version: removeCausalTs.Clock)))
                {
                    deadItemsToRemove.Add(item);
                }
            }
        }

        foreach (var item in deadItemsToRemove)
        {
            state.Removes.Remove(item);
            state.Adds.Remove(item); // Ensure we also drop the Add timestamp so it doesn't resurrect.
        }
    }

    /// <inheritdoc/>
    public void MergeAsStateCrdt(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo property)
    {
        var path = $"$.{property.JsonName}";
        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var list1 = property.Getter!(data1) as IList;
        var list2 = property.Getter!(data2) as IList;
        if (list1 is null || list2 is null) return;

        var dict1 = new Dictionary<object, object>(comparer);
        foreach (var item in list1.Cast<object>())
        {
            dict1[item] = item;
        }

        var dict2 = new Dictionary<object, object>(comparer);
        foreach (var item in list2.Cast<object>())
        {
            dict2[item] = item;
        }

        var metaState1 = GetOrUpdateState(meta1, path, comparer);
        var metaState2 = GetOrUpdateState(meta2, path, comparer);

        foreach (var (key, ts2) in metaState2.Adds)
        {
            if (!metaState1.Adds.TryGetValue(key, out var ts1) || ts2.CompareTo(ts1) > 0)
            {
                metaState1.Adds[key] = ts2;
                // If dict2 has a newer payload for this item, keep it.
                if (dict2.TryGetValue(key, out var item2))
                {
                    dict1[key] = item2;
                }
            }
        }

        foreach (var (key, ts2) in metaState2.Removes)
        {
            if (!metaState1.Removes.TryGetValue(key, out var ts1) || ts2.CompareTo(ts1) > 0)
            {
                metaState1.Removes[key] = ts2;
            }
        }

        meta1.States[path] = metaState1;

        // Rebuild list
        list1.Clear();
        foreach (var kvp in metaState1.Adds)
        {
            var itemKey = kvp.Key;
            var addTs = kvp.Value;

            if (metaState1.Removes.TryGetValue(itemKey, out var removeTs) && addTs.CompareTo(removeTs.Timestamp) <= 0)
            {
                continue; // It's removed
            }

            if (dict1.TryGetValue(itemKey, out var itemValue))
            {
                list1.Add(itemValue);
            }
        }

        SortList(list1, property);
    }
    
    private LwwSetState GetOrUpdateState(CrdtMetadata metadata, string path, IEqualityComparer<object> comparer)
    {
        if (metadata.States.TryGetValue(path, out var baseState) && baseState is LwwSetState state)
        {
            if (state.Adds is Dictionary<object, ICrdtTimestamp> dict && dict.Comparer == EqualityComparer<object>.Default)
            {
                var newAdds = new Dictionary<object, ICrdtTimestamp>(comparer);
                foreach (var kvp in state.Adds)
                {
                    if (!newAdds.TryGetValue(kvp.Key, out var existingTs) || kvp.Value.CompareTo(existingTs) > 0)
                    {
                        newAdds[kvp.Key] = kvp.Value;
                    }
                }
                
                var newRemoves = new Dictionary<object, CausalTimestamp>(comparer);
                foreach (var kvp in state.Removes)
                {
                    if (!newRemoves.TryGetValue(kvp.Key, out var existingTs) || kvp.Value.CompareTo(existingTs) > 0)
                    {
                        newRemoves[kvp.Key] = kvp.Value;
                    }
                }

                var newState = new LwwSetState(newAdds, newRemoves);
                metadata.States[path] = newState;
                return newState;
            }
            return state;
        }

        var emptyState = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(comparer), new Dictionary<object, CausalTimestamp>(comparer));
        metadata.States[path] = emptyState;
        return emptyState;
    }
    
    private bool ApplyUpsert(IList list, IDictionary<object, ICrdtTimestamp> adds, IDictionary<object, CausalTimestamp> removes, object value, CrdtOperation operation, IEqualityComparer<object> comparer)
    {
        if (removes.TryGetValue(value, out var removeCausalTs) && operation.Timestamp.CompareTo(removeCausalTs.Timestamp) < 0)
        {
            return false;
        }

        if (adds.TryGetValue(value, out var addTimestamp) && operation.Timestamp.CompareTo(addTimestamp) < 0)
        {
            return false;
        }
        
        var existing = list.Cast<object>().FirstOrDefault(i => comparer.Equals(i, value));
        if (existing is not null)
        {
            list.Remove(existing);
        }

        list.Add(value);
        adds[value] = operation.Timestamp;
        return true;
    }
    
    private bool ApplyRemove(IList list, IDictionary<object, ICrdtTimestamp> adds, IDictionary<object, CausalTimestamp> removes, object value, CrdtOperation operation, IEqualityComparer<object> comparer)
    {
        var causalTimestamp = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);

        if (removes.TryGetValue(value, out var removeCausalTs) && causalTimestamp.CompareTo(removeCausalTs) <= 0)
        {
            return false;
        }

        removes[value] = causalTimestamp;

        if (adds.TryGetValue(value, out var addTimestamp) && operation.Timestamp.CompareTo(addTimestamp) < 0)
        {
            return false; // Still returning false for the obsolete indicator even if removal logic might conceptually progress, aligned with lww metadata checks.
        }
        
        var existing = list.Cast<object>().FirstOrDefault(i => comparer.Equals(i, value));
        if (existing is not null)
        {
            list.Remove(existing);
        }
        return true;
    }
    
    private void SortList(IList list, CrdtPropertyInfo property)
    {
        var attr = property.StrategyAttribute as CrdtPriorityQueueStrategyAttribute;
        var priorityPropertyName = attr?.PriorityPropertyName ?? "Priority";

        var sortedItems = list.Cast<object>()
            .OrderByDescending(i => PocoPathHelper.GetValue(i!, priorityPropertyName, aotContexts))
            .ToList();

        list.Clear();
        foreach (var item in sortedItems)
        {
            list.Add(item);
        }
    }
}