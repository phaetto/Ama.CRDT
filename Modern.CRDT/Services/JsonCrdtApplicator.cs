using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Modern.CRDT.Services;

public sealed class JsonCrdtApplicator(ICrdtStrategyManager strategyManager) : IJsonCrdtApplicator
{
    private readonly ICrdtStrategyManager strategyManager = strategyManager ?? throw new ArgumentNullException(nameof(strategyManager));
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
    private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyCache = new();
    private static readonly Regex PathRegex = new(@"\.([^.\[\]]+)|\[(\d+)\]", RegexOptions.Compiled);

    public CrdtDocument<T> ApplyPatch<T>(CrdtDocument<T> document, CrdtPatch patch) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(patch);

        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            return new CrdtDocument<T>(document.Data, document.Metadata?.DeepClone());
        }

        var dataNode = document.Data is not null ? JsonSerializer.SerializeToNode(document.Data, SerializerOptions) : new JsonObject();
        var metaNode = document.Metadata?.DeepClone() ?? new JsonObject();

        if (dataNode is null)
        {
            return document;
        }

        foreach (var operation in patch.Operations)
        {
            var targetProperty = FindPropertyFromPath(typeof(T), operation.JsonPath);
            var strategy = strategyManager.GetStrategy(targetProperty);
            strategy.ApplyOperation(dataNode, metaNode, operation);
        }

        var finalPoco = dataNode.Deserialize<T>(SerializerOptions);
        return new CrdtDocument<T>(finalPoco, metaNode);
    }
    
    public CrdtDocument ApplyPatch(CrdtDocument document, CrdtPatch patch)
    {
        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            return new CrdtDocument(document.Data?.DeepClone(), document.Metadata?.DeepClone());
        }

        var resultData = document.Data?.DeepClone();
        var resultMeta = document.Metadata?.DeepClone();
        
        var lwwStrategy = new LwwStrategy();

        foreach (var operation in patch.Operations)
        {
            lwwStrategy.ApplyOperation(resultData ?? new JsonObject(), resultMeta ?? new JsonObject(), operation);
        }

        return new CrdtDocument(resultData, resultMeta);
    }
    
    private PropertyInfo FindPropertyFromPath(Type rootType, string jsonPath)
    {
        var cacheKey = $"{rootType.FullName}:{jsonPath}";
        if (PropertyCache.TryGetValue(cacheKey, out var property))
        {
            return property;
        }

        var segments = ParsePath(jsonPath);
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
                if (currentType.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(currentType.GetGenericTypeDefinition()))
                {
                    currentType = currentType.GetGenericArguments()[0];
                    continue;
                }
                
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

    private static List<string> ParsePath(string jsonPath)
    {
        var segments = new List<string>();
        if (string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$") return segments;
        
        var matches = PathRegex.Matches(jsonPath);
        foreach (Match match in matches.Cast<Match>())
        {
            segments.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        return segments;
    }
}