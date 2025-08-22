namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using Modern.CRDT.Services.Helpers;
using Modern.CRDT.Services.Strategies;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public sealed class JsonCrdtApplicator(ICrdtStrategyManager strategyManager) : IJsonCrdtApplicator
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
    private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyCache = new();

    public T ApplyPatch<T>(T document, CrdtPatch patch, CrdtMetadata metadata) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(metadata);

        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            var json = JsonSerializer.Serialize(document, SerializerOptions);
            return JsonSerializer.Deserialize<T>(json, SerializerOptions)!;
        }

        var dataNode = JsonSerializer.SerializeToNode(document, SerializerOptions);

        if (dataNode is null)
        {
            // Should not happen if document is not null, but as a safeguard.
            return document;
        }

        foreach (var operation in patch.Operations)
        {
            var targetProperty = FindPropertyFromPath(typeof(T), operation.JsonPath);
            var strategy = strategyManager.GetStrategy(targetProperty);

            var applied = ApplyOperationWithStateCheck(strategy, dataNode, operation, metadata);
            
            // NOTE: Add logging here for skipped operations if needed.
        }

        return dataNode.Deserialize<T>(SerializerOptions)!;
    }

    private bool ApplyOperationWithStateCheck(ICrdtStrategy strategy, JsonNode dataNode, CrdtOperation operation, CrdtMetadata metadata)
    {
        if (strategy is LwwStrategy)
        {
            metadata.LwwTimestamps.TryGetValue(operation.JsonPath, out var existingTimestamp);
            if (operation.Timestamp > existingTimestamp)
            {
                strategy.ApplyOperation(dataNode, operation);
                metadata.LwwTimestamps[operation.JsonPath] = operation.Timestamp;
                return true;
            }
        }
        else
        {
            if (metadata.SeenOperationIds.Add(operation.Timestamp))
            {
                strategy.ApplyOperation(dataNode, operation);
                return true;
            }
        }

        return false;
    }

    private PropertyInfo FindPropertyFromPath(Type rootType, string jsonPath)
    {
        var cacheKey = $"{rootType.FullName}:{jsonPath}";
        if (PropertyCache.TryGetValue(cacheKey, out var property))
        {
            return property;
        }

        var segments = JsonNodePathHelper.ParsePath(jsonPath);
        if (segments.Count == 0)
        {
            throw new ArgumentException($"Could not parse segments from path '{jsonPath}'.", nameof(jsonPath));
        }

        var currentType = rootType;
        PropertyInfo? currentProperty = null;

        foreach (var segment in segments)
        {
            if (int.TryParse(segment, out _))
            {
                if (currentType.IsArray)
                {
                    currentType = currentType.GetElementType()!;
                    continue;
                }
                
                var ienumerable = currentType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (ienumerable != null)
                {
                    currentType = ienumerable.GetGenericArguments()[0];
                    continue;
                }
                
                throw new InvalidOperationException($"Cannot determine element type for collection path segment in '{jsonPath}'.");
            }
            
            var propertyName = segment;
            
            var properties = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);

            currentProperty = properties.FirstOrDefault(p => (SerializerOptions.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name)
                .Equals(propertyName, StringComparison.OrdinalIgnoreCase));

            if (currentProperty is null)
            {
                throw new InvalidOperationException($"Property for path segment '{segment}' not found on type '{currentType.Name}' from path '{jsonPath}'.");
            }
            currentType = currentProperty.PropertyType;
        }

        if (currentProperty is null)
        {
            throw new InvalidOperationException($"Could not resolve property for path '{jsonPath}' on type '{rootType.Name}'.");
        }

        PropertyCache.TryAdd(cacheKey, currentProperty);
        return currentProperty;
    }
}