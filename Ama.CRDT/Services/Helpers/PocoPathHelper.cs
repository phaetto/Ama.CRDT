namespace Ama.CRDT.Services.Helpers;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A utility class containing helper methods for parsing JSON paths and resolving them against POCOs using Source Generator AOT Contexts.
/// </summary>
internal static class PocoPathHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    internal readonly record struct ParseResult(string JsonPath, CrdtPropertyInfo Property, Type DeclaringType);
    
    internal readonly record struct PathResolutionResult(object? Parent, CrdtPropertyInfo? Property, object? FinalSegment);

    /// <summary>
    /// Represents a dictionary key in a resolved path.
    /// </summary>
    internal readonly record struct DictionaryKeyPathSegment(string Key);

    /// <summary>
    /// Retrieves AOT metadata for the specified type strictly from the provided contexts.
    /// </summary>
    public static CrdtTypeInfo GetTypeInfo(Type type, IEnumerable<CrdtContext>? aotContexts = null)
    {
        if (aotContexts != null)
        {
            foreach (var context in aotContexts)
            {
                var info = context.GetTypeInfo(type);
                if (info != null) return info;
            }
        }

        throw new InvalidOperationException($"Type '{type.Name}' is not registered in any provided AOT context. Please ensure the type is decorated with [CrdtSerializable] and the context is registered via DI.");
    }

    /// <summary>
    /// Extracts the Document ID from a POCO by looking for an Id property.
    /// </summary>
    public static string GetDocumentId<T>(T? obj, IEnumerable<CrdtContext>? aotContexts = null)
    {
        if (obj is null) return "default";

        var type = obj.GetType();
        var typeInfo = GetTypeInfo(type, aotContexts);

        if (typeInfo.Properties.TryGetValue("Id", out var prop) && prop.CanRead)
        {
            var val = prop.Getter?.Invoke(obj);
            return val?.ToString() ?? "default";
        }

        return "default";
    }

    /// <summary>
    /// An allocation-free ref struct enumerator for splitting JSON paths.
    /// </summary>
    public ref struct PathEnumerator
    {
        private ReadOnlySpan<char> pathSpan;
        private int currentPosition;

        public PathEnumerator(ReadOnlySpan<char> path)
        {
            int startIndex = 0;
            if (path.Length > 0 && path[0] == '$') startIndex++;
            if (startIndex < path.Length && path[startIndex] == '.') startIndex++;
            pathSpan = path.Slice(startIndex);
            currentPosition = 0;
            Current = default;
        }

        public ReadOnlySpan<char> Current { get; private set; }

        public readonly PathEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            if (currentPosition >= pathSpan.Length) return false;

            int currentSegmentStart = currentPosition;
            var span = pathSpan;

            for (int i = currentPosition; i < span.Length; i++)
            {
                char c = span[i];
                if (c == '.')
                {
                    if (i > currentSegmentStart)
                    {
                        Current = span.Slice(currentSegmentStart, i - currentSegmentStart);
                        currentPosition = i + 1;
                        return true;
                    }
                    currentSegmentStart = i + 1;
                }
                else if (c == '[')
                {
                    if (i > currentSegmentStart)
                    {
                        Current = span.Slice(currentSegmentStart, i - currentSegmentStart);
                        currentPosition = i; 
                        return true;
                    }

                    var remaining = span.Slice(i);
                    int endBracket = remaining.IndexOf(']');

                    if (endBracket != -1)
                    {
                        int contentStart = 1;
                        int contentLength = endBracket - 1;

                        if (contentLength >= 2 && remaining[contentStart] == '\'' && remaining[endBracket - 1] == '\'')
                        {
                            Current = remaining.Slice(contentStart + 1, contentLength - 2);
                        }
                        else
                        {
                            Current = remaining.Slice(contentStart, contentLength);
                        }

                        i += endBracket;
                        currentPosition = i + 1;

                        if (currentPosition < span.Length && span[currentPosition] == '.')
                        {
                            currentPosition++;
                        }
                        return true;
                    }
                }
            }

            if (currentSegmentStart < span.Length)
            {
                Current = span.Slice(currentSegmentStart);
                currentPosition = span.Length;
                return true;
            }

            return false;
        }
    }

    public static PathEnumerator EnumeratePath(ReadOnlySpan<char> path)
    {
        return new PathEnumerator(path);
    }

    public static string[] ParsePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "$")
        {
            return [];
        }

        var segments = new List<string>();
        foreach (var segment in EnumeratePath(path))
        {
            segments.Add(segment.ToString());
        }

        return [.. segments];
    }
    
    public static PathResolutionResult ResolvePath(object root, string jsonPath, IEnumerable<CrdtContext>? aotContexts, bool createMissing = false)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return new PathResolutionResult(null, null, null);
        }

        var enumerator = EnumeratePath(jsonPath);
        if (!enumerator.MoveNext())
        {
            return new PathResolutionResult(null, null, null);
        }

        object? parentOfCurrent = null;
        object? currentObject = root;
        CrdtPropertyInfo? lastProperty = null;

        bool hasNext = true;
        ReadOnlySpan<char> currentSegment = enumerator.Current;

        while (true)
        {
            hasNext = enumerator.MoveNext();
            
            if (!hasNext)
            {
                break;
            }

            if (currentObject is null) return new PathResolutionResult(null, null, null);
            
            parentOfCurrent = currentObject;
            var currentTypeInfo = GetTypeInfo(currentObject.GetType(), aotContexts);

            if (currentObject is IDictionary dict)
            {
                var keyType = currentTypeInfo.DictionaryKeyType ?? typeof(object);
                var keyObj = ConvertValue(currentSegment.ToString(), keyType, aotContexts);
                
                if (keyObj != null && dict.Contains(keyObj))
                {
                    currentObject = dict[keyObj];
                    lastProperty = null; 
                }
                else if (createMissing && keyObj != null)
                {
                    var valueType = currentTypeInfo.DictionaryValueType ?? typeof(object);
                    if (valueType.IsClass && valueType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(valueType))
                    {
                        var valueTypeInfo = GetTypeInfo(valueType, aotContexts);
                        var newObj = valueTypeInfo.CreateInstance?.Invoke();
                        if (newObj != null)
                        {
                            dict[keyObj] = newObj;
                            currentObject = newObj;
                            lastProperty = null;
                        }
                        else return new PathResolutionResult(null, null, null);
                    }
                    else
                    {
                        return new PathResolutionResult(null, null, null);
                    }
                }
                else
                {
                    return new PathResolutionResult(null, null, null);
                }
            }
            else if (int.TryParse(currentSegment, out var index))
            {
                if (currentObject is IList list)
                {
                    if (index >= 0 && index < list.Count)
                    {
                        currentObject = list[index];
                        lastProperty = null; 
                    }
                    else { return new PathResolutionResult(null, null, null); } 
                }
                else { return new PathResolutionResult(null, null, null); } 
            }
            else
            {
                var currentSegmentStr = currentSegment.ToString();

                var propertyInfo = currentTypeInfo.Properties.Values.FirstOrDefault(p => 
                    p.JsonName.Equals(currentSegmentStr, StringComparison.OrdinalIgnoreCase) || 
                    p.Name.Equals(currentSegmentStr, StringComparison.OrdinalIgnoreCase));

                if (propertyInfo != null)
                {
                    lastProperty = propertyInfo;
                    var nextObject = propertyInfo.CanRead ? propertyInfo.Getter!(currentObject) : null;
                    
                    if (nextObject is null && createMissing && propertyInfo.CanWrite)
                    {
                        var propType = propertyInfo.PropertyType;
                        if (propType.IsClass && propType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(propType))
                        {
                            var propTypeInfo = GetTypeInfo(propType, aotContexts);
                            nextObject = propTypeInfo.CreateInstance?.Invoke();
                            if (nextObject != null)
                            {
                                propertyInfo.Setter!(currentObject, nextObject);
                            }
                        }
                    }
                    currentObject = nextObject;
                }
                else { return new PathResolutionResult(null, null, null); } 
            }
            
            currentSegment = enumerator.Current;
        }
        
        if (currentObject is null) return new PathResolutionResult(null, null, null);

        if (currentObject is IDictionary)
        {
            return new PathResolutionResult(parentOfCurrent ?? root, lastProperty, new DictionaryKeyPathSegment(currentSegment.ToString()));
        }

        if (int.TryParse(currentSegment, out var lastIndex))
        {
            if (currentObject is IList)
            {
                return new PathResolutionResult(parentOfCurrent ?? root, lastProperty, lastIndex);
            }
            return new PathResolutionResult(null, null, null);
        }
        
        var finalTypeInfo = GetTypeInfo(currentObject.GetType(), aotContexts);
        var finalSegmentStr = currentSegment.ToString();
        var finalProperty = finalTypeInfo.Properties.Values.FirstOrDefault(p => 
            p.JsonName.Equals(finalSegmentStr, StringComparison.OrdinalIgnoreCase) || 
            p.Name.Equals(finalSegmentStr, StringComparison.OrdinalIgnoreCase));

        if (finalProperty != null)
        {
            return new PathResolutionResult(currentObject, finalProperty, finalSegmentStr);
        }
        
        return new PathResolutionResult(null, null, null);
    }

    public static object? GetValue(object root, string jsonPath, IEnumerable<CrdtContext>? aotContexts = null)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return null;
        }

        var resolution = ResolvePath(root, jsonPath, aotContexts);

        if (resolution.Parent is null || resolution.Property is null || !resolution.Property.CanRead)
        {
            return null;
        }

        var propertyValue = resolution.Property.Getter!(resolution.Parent);
        
        if (resolution.FinalSegment is int index)
        {
            if (propertyValue is IList list && index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return null;
        }
        else if (resolution.FinalSegment is DictionaryKeyPathSegment dictKey && propertyValue is IDictionary dict)
        {
            var propTypeInfo = GetTypeInfo(resolution.Property.PropertyType, aotContexts);
            var keyObj = ConvertValue(dictKey.Key, propTypeInfo.DictionaryKeyType ?? typeof(object), aotContexts);
            if (keyObj != null && dict.Contains(keyObj))
            {
                return dict[keyObj];
            }
            return null;
        }

        return propertyValue;
    }

    public static T? GetValue<T>(object root, string jsonPath, IEnumerable<CrdtContext>? aotContexts = null)
    {
        var value = GetValue(root, jsonPath, aotContexts);
        if (value is null) return default;
        return (T?)ConvertValue(value, typeof(T), aotContexts);
    }

    public static bool SetValue(object root, string jsonPath, object? value, IEnumerable<CrdtContext>? aotContexts = null)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return false;
        }

        var resolution = ResolvePath(root, jsonPath, aotContexts);

        if (resolution.Parent is null || resolution.Property is null)
        {
            return false;
        }

        if (resolution.FinalSegment is int index)
        {
            if (resolution.Property.CanRead && resolution.Property.Getter!(resolution.Parent) is IList list)
            {
                if (index < 0 || index >= list.Count) return false;

                var propTypeInfo = GetTypeInfo(resolution.Property.PropertyType, aotContexts);
                var elementType = propTypeInfo.CollectionElementType ?? typeof(object);
                var convertedValue = ConvertValue(value, elementType, aotContexts);

                list[index] = convertedValue;
                return true;
            }
            return false;
        }
        else if (resolution.FinalSegment is DictionaryKeyPathSegment dictKey)
        {
            if (resolution.Property.CanRead && resolution.Property.Getter!(resolution.Parent) is IDictionary dict)
            {
                var propTypeInfo = GetTypeInfo(resolution.Property.PropertyType, aotContexts);
                var keyType = propTypeInfo.DictionaryKeyType ?? typeof(object);
                var valueType = propTypeInfo.DictionaryValueType ?? typeof(object);
                var keyObj = ConvertValue(dictKey.Key, keyType, aotContexts);
                if (keyObj != null)
                {
                    dict[keyObj] = ConvertValue(value, valueType, aotContexts);
                    return true;
                }
                return false;
            }
        }

        if (!resolution.Property.CanWrite)
        {
            return false;
        }

        var finalValue = ConvertValue(value, resolution.Property.PropertyType, aotContexts);
        resolution.Property.Setter!(resolution.Parent, finalValue);
        return true;
    }

    public static bool SetValue<T>(object root, string jsonPath, T value, IEnumerable<CrdtContext>? aotContexts = null)
    {
        return SetValue(root, jsonPath, (object?)value, aotContexts);
    }

    public static T ConvertTo<T>(object? value, IEnumerable<CrdtContext>? aotContexts = null)
    {
        if (value is null) return default!;
        if (value is T tVal) return tVal;
        
        var obj = ConvertValue(value, typeof(T), aotContexts);
        return obj == null ? default! : (T)obj;
    }

    public static object InstantiateCollection(Type propertyType, IEnumerable<CrdtContext>? aotContexts = null)
    {
        var typeInfo = GetTypeInfo(propertyType, aotContexts);
        
        if (typeInfo.CreateInstance != null)
        {
            return typeInfo.CreateInstance();
        }

        throw new InvalidOperationException($"Cannot instantiate collection type {propertyType.Name}. Ensure it is supported by the AOT source generator.");
    }

    public static void AddToCollection(object collection, object? item, IEnumerable<CrdtContext>? aotContexts = null)
    {
        var typeInfo = GetTypeInfo(collection.GetType(), aotContexts);
        if (typeInfo.CollectionAdd != null)
        {
            var convertedItem = ConvertValue(item, typeInfo.CollectionElementType ?? typeof(object), aotContexts);
            typeInfo.CollectionAdd(collection, convertedItem);
        }
    }

    public static void RemoveFromCollection(object collection, object? item, IEnumerable<CrdtContext>? aotContexts = null)
    {
        var typeInfo = GetTypeInfo(collection.GetType(), aotContexts);
        if (typeInfo.CollectionRemove != null)
        {
            var convertedItem = ConvertValue(item, typeInfo.CollectionElementType ?? typeof(object), aotContexts);
            typeInfo.CollectionRemove(collection, convertedItem);
        }
    }

    public static void ClearCollection(object collection, IEnumerable<CrdtContext>? aotContexts = null)
    {
        var typeInfo = GetTypeInfo(collection.GetType(), aotContexts);
        typeInfo.CollectionClear?.Invoke(collection);
    }

    public static object? ConvertValue(object? value, Type targetType, IEnumerable<CrdtContext>? aotContexts = null)
    {
        if (value is null)
        {
            return null;
        }

        if (targetType is null)
        {
            return value;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }
        
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
        {
            if (value is string s) 
            {
                return Enum.TryParse(underlyingType, s, true, out var parsedEnum) ? parsedEnum : value;
            }
            try
            {
                return Enum.ToObject(underlyingType, value);
            }
            catch
            {
                return value; 
            }
        }

        if (value is IDictionary<string, object> dictionary)
        {
            if (!underlyingType.IsPrimitive && underlyingType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(underlyingType))
            {
                var typeInfo = GetTypeInfo(underlyingType, aotContexts);
                var instance = typeInfo.CreateInstance?.Invoke();
                
                if (instance is null)
                {
                    return null;
                }

                foreach (var kvp in dictionary)
                {
                    if (typeInfo.Properties.TryGetValue(kvp.Key, out var propInfo) && propInfo.CanWrite)
                    {
                        var propValue = ConvertValue(kvp.Value, propInfo.PropertyType, aotContexts);
                        propInfo.Setter!(instance, propValue);
                    }
                }
                return instance;
            }
        }

        if (underlyingType == typeof(Guid) && value is string guidString)
        {
            return Guid.TryParse(guidString, out var parsedGuid) ? parsedGuid : value;
        }

        try
        {
            if (value is IConvertible convertible)
            {
                return convertible.ToType(underlyingType, CultureInfo.InvariantCulture);
            }
            
            return value;
        }
        catch (Exception)
        {
            return value;
        }
    }
    
    internal static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    internal static ParseResult ParseExpression<T, TProp>(Expression<Func<T, TProp>> expression, IEnumerable<CrdtContext>? aotContexts = null)
    {
        var current = expression.Body;
        
        if (current is UnaryExpression unary)
        {
            current = unary.Operand;
        }

        PropertyInfo? targetReflectionProperty = null;
        if (current is MemberExpression initialMe && initialMe.Member is PropertyInfo pi)
        {
            targetReflectionProperty = pi;
        }

        if (targetReflectionProperty is null)
        {
            throw new ArgumentException(
                "Expression must end in a property access. " +
                "If you are trying to replace an entire collection element, target the collection property instead " +
                "and use a collection-specific intent (e.g., SetIndexIntent or MapSetIntent).", nameof(expression));
        }

        var jsonPath = BuildJsonPath(current);

        var declaringType = targetReflectionProperty.DeclaringType ?? typeof(T);
        var typeInfo = GetTypeInfo(declaringType, aotContexts);
        if (typeInfo.Properties.TryGetValue(targetReflectionProperty.Name, out var crdtProperty))
        {
            return new ParseResult(jsonPath, crdtProperty, declaringType);
        }

        throw new InvalidOperationException($"Could not resolve AOT property info for {targetReflectionProperty.Name}");
    }

    private static string BuildJsonPath(Expression expression)
    {
        var parts = new List<string>();
        var current = expression;

        while (current != null)
        {
            if (current is MemberExpression me)
            {
                var propName = me.Member.Name;
                var jsonName = SerializerOptions.PropertyNamingPolicy?.ConvertName(propName) ?? propName;
                parts.Add("." + jsonName);
                current = me.Expression;
            }
            else if (current is MethodCallExpression mce && mce.Method.Name == "get_Item" && mce.Arguments.Count == 1)
            {
                var argValue = GetConstantValue(mce.Arguments[0]);
                parts.Add($"[{FormatIndex(argValue)}]");
                current = mce.Object;
            }
            else if (current is BinaryExpression be && be.NodeType == ExpressionType.ArrayIndex)
            {
                var argValue = GetConstantValue(be.Right);
                parts.Add($"[{FormatIndex(argValue)}]");
                current = be.Left;
            }
            else if (current is ParameterExpression)
            {
                break;
            }
            else
            {
                throw new ArgumentException($"Unsupported expression node type: {current.NodeType}. Ensure you only use property accesses and indexers.", nameof(expression));
            }
        }

        parts.Reverse();
        return "$" + string.Join(string.Empty, parts);
    }

    private static object? GetConstantValue(Expression expr)
    {
        if (expr is ConstantExpression ce)
        {
            return ce.Value;
        }

        var objectMember = Expression.Convert(expr, typeof(object));
        var getterLambda = Expression.Lambda<Func<object>>(objectMember);
        return getterLambda.Compile()();
    }

    private static string FormatIndex(object? index)
    {
        if (index is string s)
        {
            return $"'{s}'";
        }
        return index?.ToString() ?? string.Empty;
    }
}