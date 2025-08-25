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

        object? parentOfCurrent = null;
        object? currentObject = root;
        PropertyInfo? lastProperty = null;

        // Traverse until the second-to-last segment
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (currentObject is null) return (null, null, null);
            var segment = segments[i];

            parentOfCurrent = currentObject;

            if (int.TryParse(segment, out var index))
            {
                if (currentObject is IList list && list.Count > index)
                {
                    currentObject = list[index];
                    lastProperty = null; // We are inside an item, property context is reset
                }
                else { return (null, null, null); } // Index out of bounds
            }
            else
            {
                var properties = GetPropertiesForType(currentObject.GetType());
                if (properties.TryGetValue(segment, out var propertyInfo))
                {
                    lastProperty = propertyInfo;
                    currentObject = propertyInfo.GetValue(currentObject);
                }
                else { return (null, null, null); } // Property not found
            }
        }
        
        if (currentObject is null) return (null, null, null);

        // Now, resolve the last segment against the parent
        var lastSegment = segments.Last();
        if (int.TryParse(lastSegment, out var lastIndex))
        {
            // The target is an index in a collection.
            // currentObject is the collection. We need its parent.
            return (parentOfCurrent ?? root, lastProperty, lastIndex);
        }
        
        // The target is a property on an object.
        var parentProperties = GetPropertiesForType(currentObject.GetType());
        if (parentProperties.TryGetValue(lastSegment, out var finalProperty))
        {
            return (currentObject, finalProperty, lastSegment);
        }
        
        return (null, null, null); // Could not resolve the final segment.
    }

    public static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }
        
        if (value is JsonElement jsonElement)
        {
            return jsonElement.Deserialize(targetType, SerializerOptions);
        }
        
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType is not null)
        {
            targetType = underlyingType;
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception)
        {
            return value;
        }
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