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
    private static readonly Regex PathRegex = new(@"\.([^.\[\]]+)|\[(\d+)\]", RegexOptions.Compiled);

    /// <summary>
    /// Parses a JSON path string (e.g., "$.prop1.array[2]") into a list of segments.
    /// </summary>
    /// <param name="jsonPath">The JSON path string.</param>
    /// <returns>A list of path segments.</returns>
    public static List<string> ParsePath(string jsonPath)
    {
        var segments = new List<string>();
        if (string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$")
        {
            return segments;
        }

        var matches = PathRegex.Matches(jsonPath);
        foreach (Match match in matches.Cast<Match>())
        {
            segments.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        return segments;
    }

    /// <summary>
    /// Traverses a JsonNode structure based on a JSON path to find the parent of the final segment, creating nodes along the path if they don't exist.
    /// </summary>
    /// <param name="root">The root JsonNode to start from.</param>
    /// <param name="jsonPath">The JSON path to traverse.</param>
    /// <returns>A tuple containing the parent JsonNode and the final path segment string.</returns>
    public static (JsonNode? parent, string lastSegment) FindOrCreateParentNode(JsonNode? root, string jsonPath)
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

        var currentNode = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            if (currentNode is null) return (null, string.Empty);

            var segment = segments[i];
            JsonNode? nextNode;

            if (int.TryParse(segment, out var index))
            {
                if (currentNode is not JsonArray arr) return (null, string.Empty);

                while (arr.Count <= index) arr.Add(null);
                nextNode = arr[index];

                if (nextNode is null)
                {
                    var nextIsArrayIndex = i + 1 < segments.Count && int.TryParse(segments[i + 1], out _);
                    nextNode = nextIsArrayIndex ? new JsonArray() : new JsonObject();
                    arr[index] = nextNode;
                }
            }
            else
            {
                if (currentNode is not JsonObject obj) return (null, string.Empty);

                if (!obj.TryGetPropertyValue(segment, out nextNode) || nextNode is null)
                {
                    var nextIsArrayIndex = i + 1 < segments.Count && int.TryParse(segments[i + 1], out _);
                    nextNode = nextIsArrayIndex ? new JsonArray() : new JsonObject();
                    obj[segment] = nextNode;
                }
            }
            currentNode = nextNode;
        }

        return (currentNode, segments.Last());
    }

    /// <summary>
    /// Traverses a JsonNode structure based on a JSON path to find the parent of the final segment without modifying the structure.
    /// </summary>
    /// <param name="root">The root JsonNode to start from.</param>
    /// <param name="jsonPath">The JSON path to traverse.</param>
    /// <returns>A tuple containing the parent JsonNode and the final path segment string.</returns>
    public static (JsonNode? parent, string lastSegment) FindParentNode(JsonNode? root, string jsonPath)
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

        var currentNode = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            if (currentNode is null) return (null, string.Empty);

            currentNode = int.TryParse(segment, out var index)
                ? currentNode is JsonArray arr && arr.Count > index ? arr[index] : null
                : currentNode is JsonObject obj && obj.TryGetPropertyValue(segment, out var node) ? node : null;
        }

        return (currentNode, segments.Last());
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