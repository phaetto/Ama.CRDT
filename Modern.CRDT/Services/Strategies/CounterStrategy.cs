namespace Modern.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Modern.CRDT.Models;

/// <summary>
/// A CRDT strategy for handling numeric properties as counters.
/// It generates 'Increment' operations based on the delta between two values
/// and applies these operations by adding the delta to the current value.
/// </summary>
public sealed class CounterStrategy : ICrdtStrategy
{
    private static readonly Regex PathRegex = new(@"\.([^.\[\]]+)|\[(\d+)\]", RegexOptions.Compiled);
    
    /// <inheritdoc/>
    public void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)
    {
        var originalNumeric = GetNumericValue(originalValue);
        var modifiedNumeric = GetNumericValue(modifiedValue);

        var delta = modifiedNumeric - originalNumeric;

        if (delta == 0)
        {
            return;
        }
        
        var timestamp = GetTimestamp(modifiedMetadata) > 0 ? GetTimestamp(modifiedMetadata) : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var operation = new CrdtOperation(path, OperationType.Increment, JsonValue.Create(delta), timestamp);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation(JsonNode rootNode, JsonNode metadataNode, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(rootNode);
        ArgumentNullException.ThrowIfNull(metadataNode);
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"CounterStrategy can only handle 'Increment' operations, but received '{operation.Type}'.");
        }
        
        var (dataParent, lastSegment) = FindParent(rootNode, operation.JsonPath);
        var (metaParent, _) = FindParent(metadataNode, operation.JsonPath);

        if (dataParent is null || metaParent is null || string.IsNullOrEmpty(lastSegment)) return;
        
        var existingMetaNode = GetChildNode(metaParent, lastSegment);
        var existingTimestamp = GetTimestamp(existingMetaNode);
        
        if (operation.Timestamp <= existingTimestamp)
        {
            return;
        }

        var incrementValue = GetNumericValue(operation.Value);
        
        var existingNode = GetChildNode(dataParent, lastSegment);
        var existingValue = GetNumericValue(existingNode);
        var newValue = existingValue + incrementValue;
        SetChildNode(dataParent, lastSegment, JsonValue.Create(newValue));
        
        SetChildNode(metaParent, lastSegment, JsonValue.Create(operation.Timestamp));
    }
    
    private static (JsonNode?, string) FindParent(JsonNode? root, string jsonPath)
    {
        if (root is null || string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$")
        {
            return (root, string.Empty);
        }

        var segments = ParsePath(jsonPath);
        if (segments.Count == 0)
        {
            return (root, string.Empty);
        }

        JsonNode? currentNode = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            if (currentNode is null) return (null, string.Empty);

            if (int.TryParse(segment, out var index))
            {
                currentNode = currentNode is JsonArray arr && arr.Count > index ? arr[index] : null;
            }
            else
            {
                currentNode = currentNode is JsonObject obj && obj.TryGetPropertyValue(segment, out var node) ? node : null;
            }
        }

        return (currentNode, segments.Last());
    }

    private static List<string> ParsePath(string jsonPath)
    {
        var segments = new List<string>();
        var matches = PathRegex.Matches(jsonPath);
        foreach (Match match in matches.Cast<Match>())
        {
            segments.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        return segments;
    }
    
    private static JsonNode? GetChildNode(JsonNode parent, string segment)
    {
        if (int.TryParse(segment, out var index))
        {
            return parent is JsonArray arr && arr.Count > index ? arr[index] : null;
        }
        return parent is JsonObject obj && obj.TryGetPropertyValue(segment, out var node) ? node : null;
    }
    
    private static void SetChildNode(JsonNode parent, string segment, JsonNode? value)
    {
        if (int.TryParse(segment, out var index))
        {
            if (parent is JsonArray arr)
            {
                while (arr.Count <= index) arr.Add(null);
                arr[index] = value;
            }
        }
        else if (parent is JsonObject obj)
        {
            obj[segment] = value;
        }
    }
    
    private static long GetTimestamp(JsonNode? metaNode)
    {
        if (metaNode is JsonValue value && value.TryGetValue<long>(out var timestamp))
        {
            return timestamp;
        }
        return 0;
    }
    
    private static decimal GetNumericValue(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is not JsonValue jsonValue)
        {
            throw new InvalidOperationException($"Counter strategy requires a numeric JsonValue, but received a node of type {node.GetType().Name}.");
        }

        if (jsonValue.TryGetValue<decimal>(out var decValue)) return decValue;
        if (jsonValue.TryGetValue<int>(out var intValue)) return intValue;
        if (jsonValue.TryGetValue<long>(out var longValue)) return longValue;
        if (jsonValue.TryGetValue<double>(out var doubleValue)) return (decimal)doubleValue;
        if (jsonValue.TryGetValue<float>(out var floatValue)) return (decimal)floatValue;
        if (jsonValue.TryGetValue<short>(out var shortValue)) return shortValue;
        if (jsonValue.TryGetValue<byte>(out var byteValue)) return byteValue;

        throw new InvalidOperationException($"Counter strategy requires a numeric value, but the value '{jsonValue}' could not be converted.");
    }
}