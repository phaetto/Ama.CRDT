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
/// Implements the G-Set (Grow-Only Set) CRDT strategy.
/// In a G-Set, elements can only be added. Remove operations are ignored.
/// </summary>
[Commutative]
[Associative]
[Idempotent]
public sealed class GSetStrategy(
    IElementComparerProvider comparerProvider,
    ICrdtTimestampProvider timestampProvider,
    IOptions<CrdtOptions> options) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta)
    {
        var originalList = (originalValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedList = (modifiedValue as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();

        var elementType = property.PropertyType.IsGenericType
            ? property.PropertyType.GetGenericArguments()[0]
            : property.PropertyType.GetElementType() ?? typeof(object);
        
        var comparer = comparerProvider.GetComparer(elementType);

        var addedItems = modifiedList.Except(originalList, comparer).ToList();

        foreach (var item in addedItems)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, item, timestampProvider.Now()));
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        if (operation.Type == OperationType.Remove)
        {
            return;
        }

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IList list) return;

        var elementType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
        var itemValue = DeserializeItemValue(operation.Value, elementType);

        if (itemValue is null) return;

        var comparer = comparerProvider.GetComparer(elementType);
        if (!list.Cast<object>().Contains(itemValue, comparer))
        {
            list.Add(itemValue);
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