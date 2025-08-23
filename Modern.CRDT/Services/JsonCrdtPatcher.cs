namespace Modern.CRDT.Services;

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;

public sealed class JsonCrdtPatcher(ICrdtStrategyManager strategyManager) : IJsonCrdtPatcher
{
    private readonly ICrdtStrategyManager strategyManager = strategyManager ?? throw new ArgumentNullException(nameof(strategyManager));
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var operations = new List<CrdtOperation>();
        var fromData = from.Data is not null ? JsonSerializer.SerializeToNode(from.Data, SerializerOptions)?.AsObject() : null;
        var toData = to.Data is not null ? JsonSerializer.SerializeToNode(to.Data, SerializerOptions)?.AsObject() : null;

        DifferentiateObjectInternal("$", typeof(T), fromData, from.Metadata, toData, to.Metadata, operations);

        return new CrdtPatch(operations);
    }

    public void DifferentiateObject(string path, Type type, JsonObject? fromData, JsonObject? fromMeta, JsonObject? toData, JsonObject? toMeta, List<CrdtOperation> operations)
    {
        var properties = PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .ToArray());

        foreach (var property in properties)
        {
            var jsonPropertyName = SerializerOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            var currentPath = path == "$" ? $"$.{jsonPropertyName}" : $"{path}.{jsonPropertyName}";

            JsonNode? fromValue = null;
            fromData?.TryGetPropertyValue(jsonPropertyName, out fromValue);
            
            JsonNode? toValue = null;
            toData?.TryGetPropertyValue(jsonPropertyName, out toValue);

            JsonNode? fromMetaValue = null;
            fromMeta?.TryGetPropertyValue(jsonPropertyName, out fromMetaValue);
            
            JsonNode? toMetaValue = null;
            toMeta?.TryGetPropertyValue(jsonPropertyName, out toMetaValue);

            if (JsonNode.DeepEquals(fromValue?.DeepClone(), toValue?.DeepClone()))
            {
                continue;
            }

            var strategy = strategyManager.GetStrategy(property);

            if (strategy is LwwStrategy && property.PropertyType.IsClass && property.PropertyType != typeof(string) && !IsCollection(property.PropertyType))
            {
                DifferentiateObject(currentPath, property.PropertyType, fromValue?.AsObject(), fromMetaValue?.AsObject(), toValue?.AsObject(), toMetaValue?.AsObject(), operations);
            }
            else
            {
                strategy.GeneratePatch(this, operations, currentPath, property, fromValue, toValue, fromMetaValue, toMetaValue);
            }
        }
    }

    private void DifferentiateObjectInternal(string path, Type type, JsonObject? fromData, CrdtMetadata? fromMeta, JsonObject? toData, CrdtMetadata? toMeta, List<CrdtOperation> operations)
    {
        var properties = PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .ToArray());

        foreach (var property in properties)
        {
            var jsonPropertyName = SerializerOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            var currentPath = path == "$" ? $"$.{jsonPropertyName}" : $"{path}.{jsonPropertyName}";

            JsonNode? fromValue = null;
            fromData?.TryGetPropertyValue(jsonPropertyName, out fromValue);

            JsonNode? toValue = null;
            toData?.TryGetPropertyValue(jsonPropertyName, out toValue);

            if (JsonNode.DeepEquals(fromValue?.DeepClone(), toValue?.DeepClone()))
            {
                continue;
            }

            var strategy = strategyManager.GetStrategy(property);

            if (strategy is LwwStrategy && property.PropertyType.IsClass && property.PropertyType != typeof(string) && !IsCollection(property.PropertyType))
            {
                DifferentiateObjectInternal(currentPath, property.PropertyType, fromValue?.AsObject(), fromMeta, toValue?.AsObject(), toMeta, operations);
            }
            else
            {
                ICrdtTimestamp? fromTimestamp = null;
                fromMeta?.Lww.TryGetValue(currentPath, out fromTimestamp);

                ICrdtTimestamp? toTimestamp = null;
                toMeta?.Lww.TryGetValue(currentPath, out toTimestamp);

                var fromMetaValue = fromTimestamp is EpochTimestamp fts ? JsonValue.Create(fts.Value) : null;
                var toMetaValue = toTimestamp is EpochTimestamp tts ? JsonValue.Create(tts.Value) : null;

                strategy.GeneratePatch(this, operations, currentPath, property, fromValue, toValue, fromMetaValue, toMetaValue);
            }
        }
    }

    private static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }
}