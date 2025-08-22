namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public sealed class JsonCrdtPatcher(ICrdtStrategyManager strategyManager) : IJsonCrdtPatcher
{
    private readonly ICrdtStrategyManager strategyManager = strategyManager ?? throw new ArgumentNullException(nameof(strategyManager));
    private readonly ICrdtStrategy defaultStrategy = new LwwStrategy(); 
    private static readonly JsonSerializerOptions serializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var operations = new List<CrdtOperation>();
        var fromData = from.Data is not null ? JsonSerializer.SerializeToNode(from.Data, serializerOptions)?.AsObject() : null;
        var toData = to.Data is not null ? JsonSerializer.SerializeToNode(to.Data, serializerOptions)?.AsObject() : null;

        CompareObjectProperties("$", typeof(T), fromData, from.Metadata?.AsObject(), toData, to.Metadata?.AsObject(), operations);

        return new CrdtPatch(operations);
    }
    
    private void CompareObjectProperties(string path, Type type, JsonObject? fromData, JsonObject? fromMeta, JsonObject? toData, JsonObject? toMeta, List<CrdtOperation> operations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);

        foreach (var property in properties)
        {
            var jsonPropertyName = serializerOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            var currentPath = path == "$" ? $"$.{jsonPropertyName}" : $"{path}.{jsonPropertyName}";

            JsonNode? fromValue = null;
            fromData?.TryGetPropertyValue(jsonPropertyName, out fromValue);
            
            JsonNode? toValue = null;
            toData?.TryGetPropertyValue(jsonPropertyName, out toValue);
            
            JsonNode? fromMetaValue = null;
            fromMeta?.TryGetPropertyValue(jsonPropertyName, out fromMetaValue);
            
            JsonNode? toMetaValue = null;
            toMeta?.TryGetPropertyValue(jsonPropertyName, out toMetaValue);

            if (JsonNode.DeepEquals(fromValue?.DeepClone(), toValue?.DeepClone())) continue;

            if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                CompareObjectProperties(currentPath, property.PropertyType, fromValue?.AsObject(), fromMetaValue?.AsObject(), toValue?.AsObject(), toMetaValue?.AsObject(), operations);
            }
            else if (fromValue is JsonArray || toValue is JsonArray)
            {
                CompareJsonArrays(currentPath, fromValue?.AsArray(), fromMetaValue?.AsArray(), toValue?.AsArray(), toMetaValue?.AsArray(), operations);
            }
            else
            {
                var strategy = strategyManager.GetStrategy(property);
                operations.AddRange(strategy.GeneratePatch(currentPath, fromValue, toValue, fromMetaValue, toMetaValue));
            }
        }
    }

    private void CompareJsonArrays(string path, JsonArray? fromData, JsonArray? fromMeta, JsonArray? toData, JsonArray? toMeta, List<CrdtOperation> operations)
    {
        if (fromData is null && toData is null) return;
        
        var fromCount = fromData?.Count ?? 0;
        var toCount = toData?.Count ?? 0;
        var maxCount = Math.Max(fromCount, toCount);

        for (var i = 0; i < maxCount; i++)
        {
            var currentPath = $"{path}[{i}]";
            var fromItem = i < fromCount ? fromData?[i] : null;
            var toItem = i < toCount ? toData?[i] : null;

            var fromMetaItem = (fromMeta is not null && i < fromMeta.Count) ? fromMeta[i] : null;
            var toMetaItem = (toMeta is not null && i < toMeta.Count) ? toMeta[i] : null;
            
            operations.AddRange(defaultStrategy.GeneratePatch(currentPath, fromItem, toItem, fromMetaItem, toMetaItem));
        }
    }
}