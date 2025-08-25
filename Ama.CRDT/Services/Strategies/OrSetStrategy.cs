namespace Ama.CRDT.Services.Strategies;

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
/// Implements the OR-Set (Observed-Remove Set) CRDT strategy.
/// This set allows re-addition of elements by assigning a unique tag to each added instance.
/// </summary>
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class OrSetStrategy(
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
            var payload = new OrSetAddItem(item, Guid.NewGuid());
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, timestampProvider.Now()));
        }

        if (originalMeta.OrSets.TryGetValue(path, out var metaState))
        {
            foreach (var item in removed)
            {
                if (metaState.Adds.TryGetValue(item, out var tags) && tags.Count > 0)
                {
                    var payload = new OrSetRemoveItem(item, new HashSet<Guid>(tags));
                    operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, payload, timestampProvider.Now()));
                }
            }
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
        finally
        {
            metadata.SeenExceptions.Add(operation);
        }
    }
    
    private static void ApplyUpsert((IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, object? opValue, Type elementType)
    {
        var payload = DeserializePayload<OrSetAddItem>(opValue);
        if (payload is null) return;
        
        var itemValue = DeserializeItemValue(payload.Value.Value, elementType);
        if (itemValue is null) return;
        
        if (!state.Adds.TryGetValue(itemValue, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[itemValue] = addTags;
        }
        addTags.Add(payload.Value.Tag);
    }

    private static void ApplyRemove((IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, object? opValue, Type elementType)
    {
        var payload = DeserializePayload<OrSetRemoveItem>(opValue);
        if (payload is null) return;
        
        var itemValue = DeserializeItemValue(payload.Value.Value, elementType);
        if (itemValue is null) return;

        if (!state.Removes.TryGetValue(itemValue, out var removeTags))
        {
            removeTags = new HashSet<Guid>();
            state.Removes[itemValue] = removeTags;
        }
        foreach (var tag in payload.Value.Tags)
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
    
    private static T? DeserializePayload<T>(object? value) where T : struct
    {
        if (value is null) return null;
        if (value is T val) return val;

        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
        }
        return null;
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