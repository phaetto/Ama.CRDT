namespace Modern.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>
/// A static utility class containing shared helper methods for parsing JSON paths and manipulating JsonNode structures.
/// </summary>
internal static partial class JsonNodePathHelper
{
    // Using source generator for improved performance and more robust path parsing.
    [GeneratedRegex(@"\['([^']*)'\]|\[(\d+)\]|\.?([^\.\[]+)")]
    private static partial Regex PathRegex();

    /// <summary>
    /// Parses a JSON path string (e.g., "$.prop1['item'][2]") into an array of segments.
    /// </summary>
    /// <param name="path">The JSON path string.</param>
    /// <returns>An array of path segments.</returns>
    public static string[] ParsePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "$")
        {
            return [];
        }

        // Strip leading '$' if present.
        var pathToParse = path.StartsWith('$') ? path[1..] : path;
        var matches = PathRegex().Matches(pathToParse);
        var segments = new List<string>();

        foreach (Match match in matches.Cast<Match>())
        {
            if (match.Groups[1].Success) // Handles ['key']
            {
                segments.Add(match.Groups[1].Value);
            }
            else if (match.Groups[2].Success) // Handles [0]
            {
                segments.Add(match.Groups[2].Value);
            }
            else if (match.Groups[3].Success) // Handles .key
            {
                segments.Add(match.Groups[3].Value);
            }
        }

        return [.. segments];
    }

    /// <summary>
    /// Splits a JSON path into its parent path and the final segment.
    /// </summary>
    /// <param name="jsonPath">The JSON path string to split.</param>
    /// <returns>A tuple containing the parent path and the last segment string.</returns>
    public static (string? parentPath, string? lastSegment) SplitPath(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$")
        {
            return (null, null);
        }
    
        var lastDotIndex = jsonPath.LastIndexOf('.');
        var lastBracketIndex = jsonPath.LastIndexOf('[');
    
        if (lastBracketIndex > lastDotIndex)
        {
            // The path ends with an array accessor, like `[123]` or `['key']`.
            var parentPath = jsonPath[..lastBracketIndex];
            var segment = jsonPath[(lastBracketIndex + 1)..^1]; // remove brackets
            
            // Handle quoted segments
            if (segment.StartsWith('\'') && segment.EndsWith('\''))
            {
                segment = segment[1..^1];
            }
    
            return (string.IsNullOrEmpty(parentPath) ? "$" : parentPath, segment);
        }
    
        if (lastDotIndex > lastBracketIndex)
        {
            // The path ends with a property accessor, like `.key`.
            var parentPath = jsonPath[..lastDotIndex];
            var segment = jsonPath[(lastDotIndex + 1)..];
            
            return (string.IsNullOrEmpty(parentPath) ? "$" : parentPath, segment);
        }
    
        // This case handles a single segment path, like "myprop" or "$.myprop".
        var pathToParse = jsonPath;
        if (pathToParse.StartsWith("$."))
        {
            pathToParse = pathToParse[2..];
        }
        else if (pathToParse.StartsWith('$'))
        {
             pathToParse = pathToParse[1..];
        }
        
        if (string.IsNullOrEmpty(pathToParse))
        {
            return (null, null); // Only '$' was present.
        }
        
        return ("$", pathToParse);
    }
    
    /// <summary>
    /// Traverses a JsonNode structure based on a JSON path to find the parent of the final segment, creating nodes along the path if they don't exist.
    /// </summary>
    /// <param name="root">The root JsonNode to start from.</param>
    /// <param name="jsonPath">The JSON path to traverse.</param>
    /// <returns>A tuple containing the parent JsonNode and the final path segment string.</returns>
    public static (JsonNode? parent, string? lastSegment) FindOrCreateParentNode(JsonNode? root, string jsonPath)
    {
        if (root is null || string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$")
        {
            return (root, null);
        }

        var segments = ParsePath(jsonPath);
        if (segments.Length == 0)
        {
            return (root, null);
        }

        var currentNode = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (currentNode is null) return (null, null);

            var segment = segments[i];
            JsonNode? nextNode;

            if (int.TryParse(segment, out var index))
            {
                if (currentNode is not JsonArray arr) return (null, null);

                while (arr.Count <= index) arr.Add(null);
                nextNode = arr[index];

                if (nextNode is null)
                {
                    var nextIsArrayIndex = i + 1 < segments.Length && int.TryParse(segments[i + 1], out _);
                    nextNode = nextIsArrayIndex ? new JsonArray() : new JsonObject();
                    arr[index] = nextNode;
                }
            }
            else
            {
                if (currentNode is not JsonObject obj) return (null, null);

                if (!obj.TryGetPropertyValue(segment, out nextNode) || nextNode is null)
                {
                    var nextIsArrayIndex = i + 1 < segments.Length && int.TryParse(segments[i + 1], out _);
                    nextNode = nextIsArrayIndex ? new JsonArray() : new JsonObject();
                    obj[segment] = nextNode;
                }
            }
            currentNode = nextNode;
        }

        return (currentNode, segments[^1]);
    }

    /// <summary>
    /// Traverses a JsonNode structure based on a JSON path to find the parent of the final segment without modifying the structure.
    /// </summary>
    /// <param name="root">The root JsonNode to start from.</param>
    /// <param name="jsonPath">The JSON path to traverse.</param>
    /// <returns>A tuple containing the parent JsonNode and the final path segment string.</returns>
    public static (JsonNode? parent, string? lastSegment) FindParentNode(JsonNode? root, string jsonPath)
    {
        if (root is null || string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$")
        {
            return (root, null);
        }

        var segments = ParsePath(jsonPath);
        if (segments.Length == 0)
        {
            return (root, null);
        }

        JsonNode? current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (current is null)
            {
                return (null, null);
            }

            if (current is JsonObject obj)
            {
                obj.TryGetPropertyValue(segment, out current);
            }
            else if (current is JsonArray arr && int.TryParse(segment, out var index) && index >= 0 && index < arr.Count)
            {
                current = arr[index];
            }
            else
            {
                return (null, null);
            }
        }

        return (current, segments[^1]);
    }

    /// <summary>
    /// Gets a child node from a parent JsonNode using a segment (property name or array index).
    /// </summary>
    /// <param name="parent">The parent JsonNode.</param>
    /// <param name="segment">The property name or index-as-string.</param>
    /// <returns>The child JsonNode, or null if not found.</returns>
    public static JsonNode? GetChildNode(JsonNode parent, string segment)
    {
        return int.TryParse(segment, out var index)
            ? parent is JsonArray arr && arr.Count > index ? arr[index] : null
            : parent is JsonObject obj && obj.TryGetPropertyValue(segment, out var node) ? node : null;
    }

    /// <summary>
    /// Sets a child node on a parent JsonNode using a segment (property name or array index).
    /// </summary>
    /// <param name="parent">The parent JsonNode.</param>
    /// <param name="segment">The property name or index-as-string.</param>
    /// <param name="value">The JsonNode value to set.</param>
    public static void SetChildNode(JsonNode parent, string segment, JsonNode? value)
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

    /// <summary>
    /// Removes a child node from a parent JsonNode using a segment (property name or array index).
    /// </summary>
    /// <param name="parent">The parent JsonNode.</param>
    /// <param name="segment">The property name or index-as-string.</param>
    public static void RemoveChildNode(JsonNode parent, string segment)
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
}