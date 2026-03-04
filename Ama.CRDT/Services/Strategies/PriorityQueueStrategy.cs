namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        var originalList = originalValue as IEnumerable;
        var modifiedList = modifiedValue as IEnumerable;

        var elementType = PocoPathHelper.GetCollectionElementType(property);

        if (property.GetCustomAttribute<CrdtPriorityQueueStrategyAttribute>() is not { } attr) return;

        var comparer = comparerProvider.GetComparer(elementType);

        var originalDict = originalList?.Cast<object>().ToDictionary(item => item, item => item, comparer) ?? new Dictionary<object, object>(comparer);
        var modifiedDict = modifiedList?.Cast<object>().ToDictionary(item => item, item => item, comparer) ?? new Dictionary<object, object>(comparer);

        if (!originalMeta.PriorityQueues.TryGetValue(path, out var originalMetaState))
        {
            originalMetaState = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(comparer), new Dictionary<object, ICrdtTimestamp>(comparer));
        }
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
                var originalPriority = PocoPathHelper.GetValue(originalItem, attr.PriorityPropertyName);
                var modifiedPriority = PocoPathHelper.GetValue(modifiedItem, attr.PriorityPropertyName);

                if (!Equals(originalPriority, modifiedPriority))
                {
                    hasChanged = true;
                }
            }

            if (isNew || hasChanged)
            {
                if (originalRemoves.TryGetValue(modifiedItem, out var removeTimestamp) && changeTimestamp.CompareTo(removeTimestamp) < 0)
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

        var elementType = PocoPathHelper.GetCollectionElementType(property);

        return intent switch
        {
            AddIntent addIntent => new CrdtOperation(Guid.NewGuid(), replicaId, jsonPath, OperationType.Upsert, PocoPathHelper.ConvertValue(addIntent.Value, elementType), timestamp, clock),
            RemoveValueIntent removeIntent => new CrdtOperation(Guid.NewGuid(), replicaId, jsonPath, OperationType.Remove, PocoPathHelper.ConvertValue(removeIntent.Value, elementType), timestamp, clock),
            _ => throw new NotSupportedException($"Intent {intent.GetType().Name} is not supported for {nameof(PriorityQueueStrategy)}.")
        };
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IList list) return;
        
        var elementType = PocoPathHelper.GetCollectionElementType(property);
        
        var comparer = comparerProvider.GetComparer(elementType);
        
        if (!metadata.PriorityQueues.TryGetValue(operation.JsonPath, out var meta))
        {
            meta = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(comparer), new Dictionary<object, ICrdtTimestamp>(comparer));
            metadata.PriorityQueues[operation.JsonPath] = meta;
        }
        var adds = meta.Adds;
        var removes = meta.Removes;

        var value = PocoPathHelper.ConvertValue(operation.Value, elementType);
        if (value is null) return;
        
        switch (operation.Type)
        {
            case OperationType.Upsert:
                ApplyUpsert(list, adds, removes, value, operation.Timestamp, comparer);
                break;
            case OperationType.Remove:
                ApplyRemove(list, adds, removes, value, operation.Timestamp, comparer);
                break;
        }
        
        SortList(list, property);
    }
    
    private void ApplyUpsert(IList list, IDictionary<object, ICrdtTimestamp> adds, IDictionary<object, ICrdtTimestamp> removes, object value, ICrdtTimestamp timestamp, IEqualityComparer<object> comparer)
    {
        if (removes.TryGetValue(value, out var removeTimestamp) && timestamp.CompareTo(removeTimestamp) < 0)
        {
            return;
        }

        if (adds.TryGetValue(value, out var addTimestamp) && timestamp.CompareTo(addTimestamp) < 0)
        {
            return;
        }
        
        var existing = list.Cast<object>().FirstOrDefault(i => comparer.Equals(i, value));
        if (existing is not null)
        {
            list.Remove(existing);
        }

        list.Add(value);
        adds[value] = timestamp;
    }
    
    private void ApplyRemove(IList list, IDictionary<object, ICrdtTimestamp> adds, IDictionary<object, ICrdtTimestamp> removes, object value, ICrdtTimestamp timestamp, IEqualityComparer<object> comparer)
    {
        if (removes.TryGetValue(value, out var removeTimestamp) && timestamp.CompareTo(removeTimestamp) <= 0)
        {
            return;
        }

        removes[value] = timestamp;

        if (adds.TryGetValue(value, out var addTimestamp) && timestamp.CompareTo(addTimestamp) < 0)
        {
            return;
        }
        
        var existing = list.Cast<object>().FirstOrDefault(i => comparer.Equals(i, value));
        if (existing is not null)
        {
            list.Remove(existing);
        }
    }
    
    private void SortList(IList list, PropertyInfo property)
    {
        if (property.GetCustomAttribute<CrdtPriorityQueueStrategyAttribute>() is not { } attr) return;

        var sortedItems = list.Cast<object>()
            .OrderByDescending(i => PocoPathHelper.GetValue(i!, attr.PriorityPropertyName))
            .ToList();

        list.Clear();
        foreach (var item in sortedItems)
        {
            list.Add(item);
        }
    }
}