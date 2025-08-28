namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Ama.CRDT.Services;

/// <summary>
/// Implements the 2P-Set (Two-Phase Set) CRDT strategy.
/// In a 2P-Set, an element can be added and removed, but once removed, it cannot be re-added.
/// </summary>
[CrdtSupportedType(typeof(IEnumerable))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class TwoPhaseSetStrategy(
    IElementComparerProvider comparerProvider,
    ICrdtTimestampProvider timestampProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
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

        if (!metadata.TwoPhaseSets.TryGetValue(operation.JsonPath, out var state))
        {
            state = (new HashSet<object>(comparer), new HashSet<object>(comparer));
            metadata.TwoPhaseSets[operation.JsonPath] = state;
        }

        var itemValue = DeserializeItemValue(operation.Value, elementType);
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