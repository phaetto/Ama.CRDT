namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
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
[CrdtSupportedType(typeof(IEnumerable))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class LwwSetStrategy(
    IElementComparerProvider comparerProvider,
    ICrdtTimestampProvider timestampProvider,
    IOptions<CrdtOptions> options) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;
    
    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
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

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IList list) return;

        var elementType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
        var comparer = comparerProvider.GetComparer(elementType);

        if (!metadata.LwwSets.TryGetValue(operation.JsonPath, out var state))
        {
            var adds = new Dictionary<object, ICrdtTimestamp>(comparer);
            foreach (var item in list)
            {
                adds[item] = new EpochTimestamp(0);
            }
            state = (adds, new Dictionary<object, ICrdtTimestamp>(comparer));
            metadata.LwwSets[operation.JsonPath] = state;
        }

        var itemValue = DeserializeItemValue(operation.Value, elementType);
        if (itemValue is null) return;

        switch (operation.Type)
        {
            case OperationType.Upsert:
                if (!state.Removes.TryGetValue(itemValue, out var removeTimestamp) || operation.Timestamp.CompareTo(removeTimestamp) > 0)
                {
                    if (!state.Adds.TryGetValue(itemValue, out var removeAddTimestamp) || operation.Timestamp.CompareTo(removeAddTimestamp) > 0)
                    {
                        state.Adds[itemValue] = operation.Timestamp;
                    }
                }
                break;
            case OperationType.Remove:
                if (!state.Adds.TryGetValue(itemValue, out var addTimestamp) || operation.Timestamp.CompareTo(addTimestamp) > 0)
                {
                    if (!state.Removes.TryGetValue(itemValue, out var addRemoveTimestamp) || operation.Timestamp.CompareTo(addRemoveTimestamp) > 0)
                    {
                        state.Removes[itemValue] = operation.Timestamp;
                    }
                }
                break;
        }

        ReconstructList(list, state.Adds, state.Removes, comparer);
    }
    
    private static void ReconstructList(IList list, IDictionary<object, ICrdtTimestamp> adds, IDictionary<object, ICrdtTimestamp> removes, IEqualityComparer<object> comparer)
    {
        var liveItems = new HashSet<object>(comparer);

        foreach (var (item, addTimestamp) in adds)
        {
            if (!removes.TryGetValue(item, out var removeTimestamp) || addTimestamp.CompareTo(removeTimestamp) > 0)
            {
                liveItems.Add(item);
            }
        }
        
        // Sort the items to ensure a deterministic order across all replicas.
        // We sort by string representation as a universal, stable mechanism.
        var sortedItems = liveItems.OrderBy(i => i.ToString(), StringComparer.Ordinal).ToList();
        
        list.Clear();
        foreach (var item in sortedItems)
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