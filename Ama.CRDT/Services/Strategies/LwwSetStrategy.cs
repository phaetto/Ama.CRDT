namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// Implements the LWW-Set (Last-Writer-Wins Set) CRDT strategy.
/// An element's membership is determined by the timestamp of its last add or remove operation.
/// </summary>
public sealed class LwwSetStrategy(
    IElementComparerProvider comparerProvider,
    ICrdtTimestampProvider timestampProvider,
    IOptions<CrdtOptions> options) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta)
    {
        var originalSet = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedSet = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        
        var elementType = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments()[0] : typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        var added = modifiedSet.Except(originalSet, comparer);
        var removed = originalSet.Except(modifiedSet, comparer);

        foreach (var item in added)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, timestampProvider.Now()));
        }

        foreach (var item in removed)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, item, timestampProvider.Now()));
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.SeenExceptions.Contains(operation))
        {
            return;
        }

        try
        {
            var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
            if (parent is null || property is null || property.GetValue(parent) is not IList list) return;
            
            var elementType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
            var comparer = comparerProvider.GetComparer(elementType);

            if (!metadata.LwwSets.TryGetValue(operation.JsonPath, out var state))
            {
                state = (new Dictionary<object, ICrdtTimestamp>(comparer), new Dictionary<object, ICrdtTimestamp>(comparer));
                metadata.LwwSets[operation.JsonPath] = state;
            }

            var itemValue = DeserializeItemValue(operation.Value, elementType);
            if (itemValue is null) return;
            
            switch (operation.Type)
            {
                case OperationType.Upsert:
                    if (!state.Removes.TryGetValue(itemValue, out var removeTimestamp) || operation.Timestamp.CompareTo(removeTimestamp) > 0)
                    {
                        state.Adds[itemValue] = operation.Timestamp;
                    }
                    break;
                case OperationType.Remove:
                    if (!state.Adds.TryGetValue(itemValue, out var addTimestamp) || operation.Timestamp.CompareTo(addTimestamp) > 0)
                    {
                        state.Removes[itemValue] = operation.Timestamp;
                    }
                    break;
            }

            ReconstructList(list, state.Adds, state.Removes, comparer);
        }
        finally
        {
            metadata.SeenExceptions.Add(operation);
        }
    }
    
    private static void ReconstructList(IList list, IDictionary<object, ICrdtTimestamp> adds, IDictionary<object, ICrdtTimestamp> removes, IEqualityComparer<object> comparer)
    {
        var currentItems = new HashSet<object>(list.Cast<object>(), comparer);
        var liveItems = new HashSet<object>(comparer);

        foreach (var (item, addTimestamp) in adds)
        {
            if (!removes.TryGetValue(item, out var removeTimestamp) || addTimestamp.CompareTo(removeTimestamp) > 0)
            {
                liveItems.Add(item);
            }
        }
        
        var toAdd = liveItems.Except(currentItems, comparer).ToList();
        var toRemove = currentItems.Except(liveItems, comparer).ToList();

        foreach (var item in toRemove)
        {
            list.Remove(item);
        }

        foreach (var item in toAdd)
        {
            list.Add(item);
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