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
            return document;
        }

        var dataNode = JsonSerializer.SerializeToNode(document, SerializerOptions);

        if (dataNode is null)
        {
            return document;
        }

        foreach (var operation in patch.Operations)
        {
            var targetProperty = FindPropertyFromPath(typeof(T), operation.JsonPath);
            var strategy = strategyManager.GetStrategy(targetProperty);

            ApplyOperationWithStateCheck(strategy, dataNode, operation, metadata);
        }

        return dataNode.Deserialize<T>(SerializerOptions)!;
    }

    private bool ApplyOperationWithStateCheck(ICrdtStrategy strategy, JsonNode dataNode, CrdtOperation operation, CrdtMetadata metadata)
    {
        // 1. Idempotency Check: Is the operation already seen?
        metadata.VersionVector.TryGetValue(operation.ReplicaId, out var vectorTs);
        if (vectorTs is not null && operation.Timestamp.CompareTo(vectorTs) <= 0)
        {
            return false; // Already covered by the version vector.
        }

        if (metadata.SeenExceptions.Contains(operation))
        {
            return false; // Seen as a previous out-of-order operation.
        }

        // 2. Application Logic: Should the operation be applied based on its strategy?
        var applied = false;
        if (strategy is LwwStrategy)
        {
            metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs);
            if (lwwTs is null || operation.Timestamp.CompareTo(lwwTs) > 0)
            {
                strategy.ApplyOperation(dataNode, operation);
                metadata.Lww[operation.JsonPath] = operation.Timestamp;
                applied = true;
            }
        }
        else // For Counter, ArrayLcs, etc., apply if it's a new operation.
        {
            strategy.ApplyOperation(dataNode, operation);
            applied = true;
        }

        // 3. State Update: If applied, record it as a seen exception until the vector is advanced.
        if (applied)
        {
            metadata.SeenExceptions.Add(operation);
        }

        return applied;
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