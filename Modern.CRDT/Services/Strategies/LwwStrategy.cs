namespace Modern.CRDT.Services.Strategies;

using Modern.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>
/// Implements the Last-Writer-Wins (LWW) strategy. It generates an operation
/// only if the 'modified' value has a more recent timestamp than the 'original' value.
/// This strategy does not recurse into child nodes; it treats the given nodes as atomic values.
/// </summary>
public sealed class LwwStrategy : ICrdtStrategy
{
    private static readonly Regex PathRegex = new(@"\.([^.\[\]]+)|\[(\d+)\]", RegexOptions.Compiled);
    
    /// <inheritdoc/>
    public void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)
    {
        if (JsonNode.DeepEquals(originalValue, modifiedValue))
        {
            return;
        }

        var modifiedTimestamp = GetTimestamp(modifiedMetadata);
        var originalTimestamp = GetTimestamp(originalMetadata);

        if (modifiedTimestamp <= originalTimestamp)
        {
            return;
        }

        if (modifiedValue is null)
        {
            operations.Add(new CrdtOperation(path, OperationType.Remove, null, modifiedTimestamp));
        }
        else
        {
            operations.Add(new CrdtOperation(path, OperationType.Upsert, modifiedValue.DeepClone(), modifiedTimestamp));
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(JsonNode rootNode, JsonNode metadataNode, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(rootNode);
        ArgumentNullException.ThrowIfNull(metadataNode);
        ArgumentNullException.ThrowIfNull(operation);

        var (dataParent, lastSegment) = FindParent(rootNode, operation.JsonPath);
        var (metaParent, _) = FindParent(metadataNode, operation.JsonPath);

        if (dataParent is null || metaParent is null || string.IsNullOrEmpty(lastSegment)) return;
        
        var existingMetaNode = GetChildNode(metaParent, lastSegment);
        var existingTimestamp = GetTimestamp(existingMetaNode);
        
        if (operation.Timestamp <= existingTimestamp)
        {
            return;
        }
        
        if (operation.Type == OperationType.Remove)
        {
            RemoveChildNode(dataParent, lastSegment);
            RemoveChildNode(metaParent, lastSegment);
        }
        else if (operation.Type == OperationType.Upsert)
        {
            SetChildNode(dataParent, lastSegment, operation.Value?.DeepClone());
            SetChildNode(metaParent, lastSegment, JsonValue.Create(operation.Timestamp));
        }
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
                if (currentNode is not JsonArray arr)
                {
                    arr = new JsonArray();
                    if (currentNode is JsonObject obj) obj[segment] = arr;
                }
                while (arr.Count <= index) arr.Add(null);
                currentNode = arr[index];
            }
            else
            {
                if (currentNode is not JsonObject obj) return (null, string.Empty);
                if (!obj.TryGetPropertyValue(segment, out var nextNode) || nextNode is null)
                {
                    var nextIsArrayIndex = i + 1 < segments.Count && int.TryParse(segments[i + 1], out _);
                    nextNode = nextIsArrayIndex ? new JsonArray() : new JsonObject();
                    obj[segment] = nextNode;
                }
                currentNode = nextNode;
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
    
    private static void RemoveChildNode(JsonNode parent, string segment)
    {
        if (int.TryParse(segment, out var index))
        {
            if (parent is JsonArray arr && index < arr.Count) arr.RemoveAt(index);
        }
        else if (parent is JsonObject obj)
        {
            obj.Remove(segment);
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
}