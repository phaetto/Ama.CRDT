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
/// Implements the LWW-Map (Last-Writer-Wins Map) CRDT strategy.
/// Each key-value pair is treated as an independent LWW-Register.
/// </summary>
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class LwwMapStrategy(
    IElementComparerProvider comparerProvider,
    ICrdtTimestampProvider timestampProvider,
    IOptions<CrdtOptions> options) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        var originalDict = originalValue as IDictionary;
        var modifiedDict = modifiedValue as IDictionary;

        if (originalDict is null && modifiedDict is null) return;

        var keyType = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments()[0] : typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);

        var originalKeys = originalDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);
        var modifiedKeys = modifiedDict?.Keys.Cast<object>().ToHashSet(comparer) ?? new HashSet<object>(comparer);
        
        var addedKeys = modifiedKeys.Except(originalKeys, comparer);
        var removedKeys = originalKeys.Except(modifiedKeys, comparer);
        var commonKeys = originalKeys.Intersect(modifiedKeys, comparer);

        foreach (var key in addedKeys)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new KeyValuePair<object, object?>(key, modifiedDict![key]), timestampProvider.Now()));
        }

        foreach (var key in removedKeys)
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new KeyValuePair<object, object?>(key, null), timestampProvider.Now()));
        }

        foreach (var key in commonKeys)
        {
            var originalItemValue = originalDict![key];
            var modifiedItemValue = modifiedDict![key];

            if (!Equals(originalItemValue, modifiedItemValue))
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new KeyValuePair<object, object?>(key, modifiedItemValue), timestampProvider.Now()));
            }
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IDictionary dict) return;

        var keyType = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments()[0] : typeof(object);
        var valueType = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments()[1] : typeof(object);
        var comparer = comparerProvider.GetComparer(keyType);

        if (!metadata.LwwMaps.TryGetValue(operation.JsonPath, out var timestamps))
        {
            timestamps = new Dictionary<object, ICrdtTimestamp>(comparer);
            metadata.LwwMaps[operation.JsonPath] = timestamps;
        }

        KeyValuePair<object, object?>? payload = DeserializePayload<KeyValuePair<object, object?>>(operation.Value);
        if (!payload.HasValue)
        {
            return;
        }

        var payloadValue = payload.Value;
        var itemKey = DeserializeValue(payloadValue.Key, keyType);
        if (itemKey is null)
        {
            return;
        }
        
        if (timestamps.TryGetValue(itemKey, out var currentTimestamp) && operation.Timestamp.CompareTo(currentTimestamp) <= 0)
        {
            return;
        }

        timestamps[itemKey] = operation.Timestamp;

        switch (operation.Type)
        {
            case OperationType.Upsert:
                var itemValue = DeserializeValue(payloadValue.Value, valueType);
                dict[itemKey] = itemValue;
                break;
            case OperationType.Remove:
                dict.Remove(itemKey);
                break;
        }
    }

    private static T? DeserializePayload<T>(object? value)
    {
        if (value is null) return default;
        if (value is T val) return val;

        if (value is JsonElement jsonElement)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            catch (JsonException) { /* Fallback for KeyValuePair which can be tricky to deserialize */ }
        }

        if (typeof(T) == typeof(KeyValuePair<object, object?>) && value is JsonElement kvpJson)
        {
            var key = kvpJson.TryGetProperty("Key", out var k) ? JsonSerializer.Deserialize<object>(k.GetRawText()) : null;
            var pairValue = kvpJson.TryGetProperty("Value", out var v) ? JsonSerializer.Deserialize<object?>(v.GetRawText()) : null;
            if (key is not null)
                return (T)(object)new KeyValuePair<object, object?>(key, pairValue);
        }
        return default;
    }

    private static object? DeserializeValue(object? value, Type targetType)
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