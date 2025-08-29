namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Ama.CRDT.Services;

/// <summary>
/// Implements the G-Set (Grow-Only Set) CRDT strategy.
/// In a G-Set, elements can only be added. Remove operations are ignored.
/// </summary>
[CrdtSupportedType(typeof(IList))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class GSetStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalList = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedList = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        
        var comparer = comparerProvider.GetComparer(elementType);

        var addedItems = modifiedList.Except(originalList, comparer).ToList();

        foreach (var item in addedItems)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, changeTimestamp));
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type == OperationType.Remove)
        {
            return;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IList list) return;

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        var itemValue = DeserializeItemValue(operation.Value, elementType);

        if (itemValue is null) return;

        var comparer = comparerProvider.GetComparer(elementType);
        var currentItems = list.Cast<object>().ToList();
        
        if (!currentItems.Contains(itemValue, comparer))
        {
            currentItems.Add(itemValue);
            
            // Sort the items to ensure a deterministic order across all replicas.
            var sortedItems = currentItems.OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
            
            list.Clear();
            foreach (var item in sortedItems)
            {
                list.Add(item);
            }
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
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }
}