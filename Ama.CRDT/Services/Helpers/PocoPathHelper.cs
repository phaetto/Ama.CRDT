namespace Ama.CRDT.Services.Helpers;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// <summary>
/// A utility class containing helper methods for parsing JSON paths and resolving them against POCOs using reflection.
/// </summary>
internal static partial class PocoPathHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> PropertyCache = new();

    [GeneratedRegex(@"\['([^']*)'\]|\[(\d+)\]|\.?([^\.\[]+)")]
    private static partial Regex PathRegex();
    
    public static string[] ParsePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "$")
        {
            return [];
        }

        var pathToParse = path.StartsWith('$') ? path[1..] : path;
        var matches = PathRegex().Matches(pathToParse);
        var segments = new List<string>();

        foreach (Match match in matches.Cast<Match>())
        {
            if (match.Groups[1].Success)
            {
                segments.Add(match.Groups[1].Value);
            }
            else if (match.Groups[2].Success)
            {
                segments.Add(match.Groups[2].Value);
            }
            else if (match.Groups[3].Success)
            {
                segments.Add(match.Groups[3].Value);
            }
        }

        return [.. segments];
    }
    
    public static (object? parent, PropertyInfo? property, object? finalSegment) ResolvePath(object root, string jsonPath)
    {
        var segments = ParsePath(jsonPath);
        if (segments.Length == 0)
        {
            return (null, null, null);
        }

        object? parent = root;

        // Traverse until the second-to-last segment to find the direct parent of the target
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (parent is null) return (null, null, null);
            var segment = segments[i];

            if (int.TryParse(segment, out var index))
            {
                if (parent is IList list && list.Count > index)
                {
                    parent = list[index];
                }
                else { return (null, null, null); }
            }
            else
            {
                var properties = GetPropertiesForType(parent.GetType());
                if (properties.TryGetValue(segment, out var propertyInfo))
                {
                    parent = propertyInfo.GetValue(parent);
                }
                else { return (null, null, null); }
            }
        }
        
        if (parent is null) return (null, null, null);

        // Now, resolve the last segment against the direct parent
        var lastSegment = segments.Last();
        if (int.TryParse(lastSegment, out var lastIndex))
        {
            // The parent should be a list, and we are targeting an index.
            // There is no property, just the index on the parent (list).
            return (parent, null, lastIndex);
        }
        
        // The parent is an object, and we are targeting a property by name.
        var parentType = parent.GetType();
        var parentProperties = GetPropertiesForType(parentType);
        if (parentProperties.TryGetValue(lastSegment, out var finalProperty))
        {
            return (parent, finalProperty, lastSegment);
        }
        
        return (null, null, null); // Could not resolve the final segment.
    }
    
    private static IReadOnlyDictionary<string, PropertyInfo> GetPropertiesForType(Type type)
    {
        return PropertyCache.GetOrAdd(type, t =>
        {
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);

            var dict = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in props)
            {
                // Add mapping for camelCase name
                var camelCaseName = SerializerOptions.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name;
                dict[camelCaseName] = p;
                
                // Add mapping for original PascalCase name to be safe
                dict[p.Name] = p;
            }

            return dict;
        });
    }
}