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
using System.Text.Json;
using Ama.CRDT.Services;

/// <summary>
/// Implements a Max-Wins Map strategy. For each key, conflicts are resolved by choosing the highest value.
/// This strategy makes the map's keys grow-only; removals are not propagated.
/// </summary>
[CrdtSupportedType(typeof(IDictionary))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class MaxWinsMapStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalDict = originalValue as IDictionary;
        var modifiedDict = modifiedValue as IDictionary;

        if (originalDict is null && modifiedDict is null) return;

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var comparer = comparerProvider.GetComparer(keyType);

        var allKeys = (originalDict?.Keys.Cast<object>() ?? Enumerable.Empty<object>())
            .Union(modifiedDict?.Keys.Cast<object>() ?? Enumerable.Empty<object>(), comparer)
            .ToHashSet(comparer);
        
        foreach (var key in allKeys)
        {
            var originalExists = originalDict?.Contains(key) ?? false;
            var modifiedExists = modifiedDict?.Contains(key) ?? false;

            if (originalExists && !modifiedExists) continue;

            var originalItemValue = originalExists ? originalDict![key] : null;
            var modifiedItemValue = modifiedExists ? modifiedDict![key] : null;

            if (modifiedExists && (originalItemValue is null || (originalItemValue is IComparable o && o.CompareTo(modifiedItemValue) < 0)))
            {
                var payload = new KeyValuePair<object, object?>(key, modifiedItemValue);
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp));
            }
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Upsert) return;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IDictionary dict) return;
        
        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);

        var payload = DeserializePayload<KeyValuePair<object, object?>>(operation.Value);
        
        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null) return;

        var incomingValue = PocoPathHelper.ConvertValue(payload.Value, valueType);
        if (incomingValue is not IComparable) return;
        
        if (dict.Contains(itemKey))
        {
            var currentValue = dict[itemKey];
            if (currentValue is IComparable currentComparable && currentComparable.CompareTo(incomingValue) < 0)
            {
                dict[itemKey] = incomingValue;
            }
        }
        else
        {
            dict[itemKey] = incomingValue;
        }
    }
    
    private static T? DeserializePayload<T>(object? value)
    {
        if (value is null) return default;
        if (value is T val) return val;

        if (value is JsonElement jsonElement)
        {
            if (typeof(T) == typeof(KeyValuePair<object, object?>))
            {
                var key = jsonElement.TryGetProperty("Key", out var k) ? JsonSerializer.Deserialize<object>(k.GetRawText()) : null;
                var pairValue = jsonElement.TryGetProperty("Value", out var v) ? JsonSerializer.Deserialize<object?>(v.GetRawText()) : null;
                if (key is not null)
                    return (T)(object)new KeyValuePair<object, object?>(key, pairValue);
            }
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
        }
        return default;
    }
}