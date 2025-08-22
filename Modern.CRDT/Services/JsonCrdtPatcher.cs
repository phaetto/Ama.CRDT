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
using System.Collections;

public sealed class JsonCrdtPatcher(ICrdtStrategyManager strategyManager) : IJsonCrdtPatcher
{
    private readonly ICrdtStrategyManager strategyManager = strategyManager ?? throw new ArgumentNullException(nameof(strategyManager));
    private static readonly JsonSerializerOptions serializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var operations = new List<CrdtOperation>();
        var fromData = from.Data is not null ? JsonSerializer.SerializeToNode(from.Data, serializerOptions)?.AsObject() : null;
        var toData = to.Data is not null ? JsonSerializer.SerializeToNode(to.Data, serializerOptions)?.AsObject() : null;

        DifferentiateObject("$", typeof(T), fromData, from.Metadata?.AsObject(), toData, to.Metadata?.AsObject(), operations);

        return new CrdtPatch(operations);
    }
    
    public void DifferentiateObject(string path, Type type, JsonObject? fromData, JsonObject? fromMeta, JsonObject? toData, JsonObject? toMeta, List<CrdtOperation> operations)
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
    
    private static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }
}