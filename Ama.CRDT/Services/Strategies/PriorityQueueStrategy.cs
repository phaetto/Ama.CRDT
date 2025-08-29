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
using System.Reflection;
using System.Text.Json;
using Ama.CRDT.Services;

/// <inheritdoc/>
[CrdtSupportedType(typeof(IList))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class PriorityQueueStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalList = originalValue as IEnumerable;
        var modifiedList = modifiedValue as IEnumerable;

        var elementType = PocoPathHelper.GetCollectionElementType(property);

        var comparer = comparerProvider.GetComparer(elementType);

        var originalDict = originalList?.Cast<object>().ToDictionary(item => item, item => item, comparer) ?? new Dictionary<object, object>(comparer);
        var modifiedDict = modifiedList?.Cast<object>().ToDictionary(item => item, item => item, comparer) ?? new Dictionary<object, object>(comparer);

        if (!originalMeta.PriorityQueues.TryGetValue(path, out var originalMetaTuple))
        {
            originalMetaTuple = (new Dictionary<object, ICrdtTimestamp>(comparer), new Dictionary<object, ICrdtTimestamp>(comparer));
        }
        var (originalAdds, originalRemoves) = originalMetaTuple;

        // Find removals
        foreach (var item in originalDict.Values.Where(o => !modifiedDict.ContainsKey(o)))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item, changeTimestamp));
        }

        // Find additions and updates
        foreach (var modifiedItem in modifiedDict.Values)
        {
            var isNew = !originalDict.TryGetValue(modifiedItem, out var originalItem);
            var hasChanged = !isNew && !JsonSerializer.Serialize(originalItem).Equals(JsonSerializer.Serialize(modifiedItem));

            if (isNew || hasChanged)
            {
                if (originalRemoves.TryGetValue(modifiedItem, out var removeTimestamp) && changeTimestamp.CompareTo(removeTimestamp) < 0)
                {
                    continue;
                }

                if (!isNew && originalAdds.TryGetValue(originalItem!, out var addTimestamp) && changeTimestamp.CompareTo(addTimestamp) < 0)
                {
                    continue;
                }

                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedItem, changeTimestamp));
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
        
        if (!metadata.PriorityQueues.TryGetValue(operation.JsonPath, out var meta))
        {
            meta = (new Dictionary<object, ICrdtTimestamp>(comparer), new Dictionary<object, ICrdtTimestamp>(comparer));
            metadata.PriorityQueues[operation.JsonPath] = meta;
        }
        var (adds, removes) = meta;

        var value = DeserializeItemValue(operation.Value, elementType);
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
    
    private static object? DeserializeItemValue(object? value, Type targetType)
    {
        if (value is null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    private void SortList(IList list, PropertyInfo property)
    {
        if (property.GetCustomAttribute<CrdtPriorityQueueStrategyAttribute>() is not { } attr) return;

        var sortedItems = list.Cast<object>()
            .OrderBy(i => i?.GetType().GetProperty(attr.PriorityPropertyName)?.GetValue(i))
            .ToList();

        list.Clear();
        foreach (var item in sortedItems)
        {
            list.Add(item);
        }
    }
}