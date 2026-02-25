namespace Ama.CRDT.Services.Helpers;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// A utility class containing helper methods for parsing JSON paths and resolving them against POCOs using reflection.
/// </summary>
internal static partial class PocoPathHelper
{
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
                if (currentObject is IList list)
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
            if (currentObject is IList)
            {
                // The path resolves to an element in a collection. We return information
                // about the collection's property, which is needed for strategy resolution.
                // We do not check array bounds, as the operation might be an insert.
                return (parentOfCurrent ?? root, lastProperty, lastIndex);
            }

            return (null, null, null);
        }
        
        // The target is a property on an object.
        var parentProperties = GetPropertiesForType(currentObject.GetType());
        if (parentProperties.TryGetValue(lastSegment, out var finalProperty))
        {
            return (currentObject, finalProperty, lastSegment);
        }
        
        return (null, null, null); // Could not resolve the final segment.
    }

    public static object? GetValue(object root, string jsonPath)
    {
        var (parent, property, finalSegment) = ResolvePath(root, jsonPath);

        if (parent is null || property is null)
        {
            return null;
        }

        var propertyValue = property.GetValue(parent);
        if (finalSegment is int index)
        {
            if (propertyValue is IList list && index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return null; // Index out of bounds or not a list
        }

        return propertyValue;
    }

    public static bool SetValue(object root, string jsonPath, object? value)
    {
        var (parent, property, finalSegment) = ResolvePath(root, jsonPath);

        if (parent is null || property is null || !property.CanWrite)
        {
            return false;
        }

        if (finalSegment is int index)
        {
            if (property.GetValue(parent) is IList list)
            {
                if (index < 0 || index >= list.Count) return false;

                var elementType = GetCollectionElementType(property);
                var convertedValue = ConvertValue(value, elementType);

                list[index] = convertedValue;
                return true;
            }
            return false;
        }

        var finalValue = ConvertValue(value, property.PropertyType);
        property.SetValue(parent, finalValue);
        return true;
    }

    public static Type GetCollectionElementType(PropertyInfo property)
    {
        var type = property.PropertyType;
        return type.IsGenericType
            ? type.GetGenericArguments()[0]
            : type.GetElementType() ?? typeof(object);
    }

    public static Type GetDictionaryKeyType(PropertyInfo property)
    {
        var type = property.PropertyType;

        // Check if the type is or implements IDictionary<,>
        var genericDictionaryInterface = (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (genericDictionaryInterface is not null)
        {
            return genericDictionaryInterface.GetGenericArguments()[0];
        }
        
        // Fallback for non-generic IDictionary, as we cannot determine the key type.
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return typeof(object);
        }

        return typeof(object);
    }

    public static Type GetDictionaryValueType(PropertyInfo property)
    {
        var type = property.PropertyType;

        // Check if the type is or implements IDictionary<,>
        var genericDictionaryInterface = (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (genericDictionaryInterface is not null)
        {
            return genericDictionaryInterface.GetGenericArguments()[1];
        }

        // Fallback for non-generic IDictionary, as we cannot determine the value type.
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return typeof(object);
        }

        return typeof(object);
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
        
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
        {
            if (value is string s) return Enum.Parse(underlyingType, s, true);
            return Enum.ToObject(underlyingType, value);
        }

        if (value is IDictionary<string, object> dictionary)
        {
            if (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyType = underlyingType.GetGenericArguments()[0];
                var valueType = underlyingType.GetGenericArguments()[1];

                dictionary.TryGetValue("Key", out var keyObj);
                dictionary.TryGetValue("Value", out var valueObj);

                var key = ConvertValue(keyObj, keyType);
                var val = ConvertValue(valueObj, valueType);

                return Activator.CreateInstance(underlyingType, key, val);
            }

            if (!underlyingType.IsPrimitive && underlyingType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(underlyingType))
            {
                var instance = Activator.CreateInstance(underlyingType);
                if (instance is null)
                {
                    return null;
                }
                var properties = GetPropertiesForType(underlyingType);

                foreach (var kvp in dictionary)
                {
                    if (properties.TryGetValue(kvp.Key, out var propInfo) && propInfo.CanWrite)
                    {
                        var propValue = ConvertValue(kvp.Value, propInfo.PropertyType);
                        propInfo.SetValue(instance, propValue);
                    }
                }
                return instance;
            }
        }

        try
        {
            return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
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
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            var dict = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in props)
            {
                dict[p.Name] = p;
            }

            return dict;
        });
    }
}