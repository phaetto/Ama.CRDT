namespace Ama.CRDT.Services.Helpers;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A utility class containing helper methods for parsing JSON paths and resolving them against POCOs using reflection.
/// </summary>
internal static class PocoPathHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();
    private static readonly ConcurrentDictionary<Type, CachedPropertyMetadata[]> MetadataPropertyCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, PropertyAccessor> AccessorCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> DocumentIdPropertyCache = new();
    
    // Type resolution caches to avoid repetitive LINQ and reflection overhead
    private static readonly ConcurrentDictionary<Type, Type> CollectionElementTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Type> DictionaryKeyTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Type> DictionaryValueTypeCache = new();

    // Expression compilation caches to replace Activator.CreateInstance
    private static readonly ConcurrentDictionary<Type, Func<object>> ConstructorCache = new();
    private static readonly ConcurrentDictionary<Type, Func<object?, object?, object>> KvpConstructorCache = new();

    private static readonly ConcurrentDictionary<Type, Action<object, object?>> CollectionAdders = new();
    private static readonly ConcurrentDictionary<Type, Action<object, object?>> CollectionRemovers = new();
    private static readonly ConcurrentDictionary<Type, Action<object>> CollectionClearers = new();

    internal readonly record struct ParseResult(string JsonPath, PropertyInfo Property);

    /// <summary>
    /// Represents a dictionary key in a resolved path.
    /// </summary>
    internal readonly record struct DictionaryKeyPathSegment(string Key);

    internal sealed class CachedPropertyMetadata(PropertyInfo property, string jsonPropertyName)
    {
        public PropertyInfo Property { get; } = property;
        public string PathSuffix { get; } = $".{jsonPropertyName}";
        public string RootedPath { get; } = $"$.{jsonPropertyName}";
        public PropertyAccessor Accessor { get; } = GetAccessor(property);
    }

    /// <summary>
    /// Extracts the Document ID from a POCO by looking for a PartitionKey attribute or an Id property.
    /// </summary>
    public static string GetDocumentId<T>(T? obj)
    {
        if (obj is null) return "default";

        var type = obj.GetType();
        var prop = DocumentIdPropertyCache.GetOrAdd(type, t =>
        {
            var attr = t.GetCustomAttribute<Attributes.PartitionKeyAttribute>();
            if (attr != null)
            {
                return t.GetProperty(attr.PropertyName, BindingFlags.Public | BindingFlags.Instance);
            }
            return t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        });

        if (prop != null)
        {
            var val = GetAccessor(prop).Getter(obj);
            return val?.ToString() ?? "default";
        }

        return "default";
    }

    /// <summary>
    /// Represents a heavily optimized, pre-compiled accessor for a PropertyInfo 
    /// to eliminate boxing and reflection overhead.
    /// </summary>
    internal sealed class PropertyAccessor
    {
        public PropertyInfo Property { get; }
        public Func<object, object?> Getter { get; }
        public Action<object, object?> Setter { get; }

        public PropertyAccessor(PropertyInfo property)
        {
            Property = property;
            var declaringType = property.DeclaringType ?? property.ReflectedType!;

            var instanceParam = Expression.Parameter(typeof(object), "obj");
            var castInstance = Expression.Convert(instanceParam, declaringType);
            var propertyAccess = Expression.Property(castInstance, property);
            
            // Compile Getter
            var castProperty = Expression.Convert(propertyAccess, typeof(object));
            Getter = Expression.Lambda<Func<object, object?>>(castProperty, instanceParam).Compile();

            // Compile Setter
            if (property.CanWrite)
            {
                var valueParam = Expression.Parameter(typeof(object), "val");
                var castValue = Expression.Convert(valueParam, property.PropertyType);
                var assign = Expression.Assign(propertyAccess, castValue);
                Setter = Expression.Lambda<Action<object, object?>>(assign, instanceParam, valueParam).Compile();
            }
            else
            {
                Setter = (_, _) => throw new NotSupportedException($"Property '{property.Name}' on type '{declaringType.Name}' does not have a setter.");
            }
        }
    }

    /// <summary>
    /// A generic converter that dynamically compiles the exact IL instructions needed
    /// to cast or convert between a runtime type and T.
    /// </summary>
    internal static class GenericConverter<T>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, T>> Converters = new();

        public static T Convert(object value)
        {
            var type = value.GetType();
            var converter = Converters.GetOrAdd(type, t =>
            {
                var param = Expression.Parameter(typeof(object), "val");
                var castParam = Expression.Convert(param, t);
                var body = BuildSafeConversion(castParam, t, typeof(T));
                return Expression.Lambda<Func<object, T>>(body, param).Compile();
            });
            return converter(value);
        }

        internal static Expression BuildSafeConversion(Expression source, Type sourceType, Type targetType)
        {
            if (sourceType == targetType) return source;

            // If the source is an object/interface, we can't emit a direct unboxing conversion
            // without knowing the exact boxed type, so we safely fall back to ConvertValue.
            if (sourceType == typeof(object) || sourceType == typeof(ValueType) || sourceType.IsInterface)
            {
                return BuildConvertValueCall(source, targetType);
            }

            try
            {
                // Let the Expression engine build the best direct conversion (e.g. IL conv.r8 for int -> decimal)
                return Expression.Convert(source, targetType);
            }
            catch (InvalidOperationException)
            {
                // Fallback for types that lack an implicit/explicit cast operator (like string -> int)
                return BuildConvertValueCall(source, targetType);
            }
        }

        private static Expression BuildConvertValueCall(Expression source, Type targetType)
        {
            var convertValueMethod = typeof(PocoPathHelper).GetMethod(nameof(ConvertValue), BindingFlags.Public | BindingFlags.Static)!;
            var sourceAsObject = Expression.Convert(source, typeof(object));
            var targetTypeConst = Expression.Constant(targetType, typeof(Type));
            var callConvertValue = Expression.Call(convertValueMethod, sourceAsObject, targetTypeConst);
            return Expression.Convert(callConvertValue, targetType);
        }
    }

    /// <summary>
    /// A strongly-typed, compiled property accessor cache to completely eliminate 
    /// boxing and runtime conversion overhead when getting and setting value types.
    /// </summary>
    internal static class GenericPropertyAccessor<T>
    {
        private static readonly ConcurrentDictionary<PropertyInfo, Func<object, T>> Getters = new();
        private static readonly ConcurrentDictionary<PropertyInfo, Action<object, T>> Setters = new();

        public static Func<object, T> GetGetter(PropertyInfo property)
        {
            return Getters.GetOrAdd(property, p =>
            {
                var declaringType = p.DeclaringType ?? p.ReflectedType!;
                var instanceParam = Expression.Parameter(typeof(object), "obj");
                var castInstance = Expression.Convert(instanceParam, declaringType);
                var propAccess = Expression.Property(castInstance, p);
                
                var convertedProp = GenericConverter<T>.BuildSafeConversion(propAccess, p.PropertyType, typeof(T));
                return Expression.Lambda<Func<object, T>>(convertedProp, instanceParam).Compile();
            });
        }

        public static Action<object, T> GetSetter(PropertyInfo property)
        {
            return Setters.GetOrAdd(property, p =>
            {
                if (!p.CanWrite) return (_, _) => throw new InvalidOperationException($"Property '{p.Name}' does not have a setter.");

                var declaringType = p.DeclaringType ?? p.ReflectedType!;
                var instanceParam = Expression.Parameter(typeof(object), "obj");
                var valueParam = Expression.Parameter(typeof(T), "val");
                var castInstance = Expression.Convert(instanceParam, declaringType);
                
                var convertedValue = GenericConverter<T>.BuildSafeConversion(valueParam, typeof(T), p.PropertyType);
                var assign = Expression.Assign(Expression.Property(castInstance, p), convertedValue);
                
                return Expression.Lambda<Action<object, T>>(assign, instanceParam, valueParam).Compile();
            });
        }
    }

    public static PropertyAccessor GetAccessor(PropertyInfo property)
    {
        return AccessorCache.GetOrAdd(property, p => new PropertyAccessor(p));
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
                        currentPosition = i; // Do not advance past '[', handle it in the next MoveNext
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
    
    public static (object? parent, PropertyInfo? property, object? finalSegment) ResolvePath(object root, string jsonPath, bool createMissing = false)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return (null, null, null);
        }

        var enumerator = EnumeratePath(jsonPath);
        if (!enumerator.MoveNext())
        {
            return (null, null, null);
        }

        object? parentOfCurrent = null;
        object? currentObject = root;
        PropertyInfo? lastProperty = null;

        bool hasNext = true;
        ReadOnlySpan<char> currentSegment = enumerator.Current;

        while (true)
        {
            hasNext = enumerator.MoveNext();
            
            if (!hasNext)
            {
                // currentSegment is the last segment
                break;
            }

            // Not the last segment, process currentSegment against currentObject
            if (currentObject is null) return (null, null, null);
            
            parentOfCurrent = currentObject;

            if (currentObject is IDictionary dict)
            {
                var keyType = GetDictionaryKeyType(currentObject.GetType());
                var keyObj = ConvertValue(currentSegment.ToString(), keyType);
                
                if (keyObj != null && dict.Contains(keyObj))
                {
                    currentObject = dict[keyObj];
                    lastProperty = null; // We are inside an item, property context is reset
                }
                else if (createMissing && keyObj != null)
                {
                    var valueType = GetDictionaryValueType(currentObject.GetType());
                    if (valueType.IsClass && valueType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(valueType))
                    {
                        var factory = ConstructorCache.GetOrAdd(valueType, CreateConstructor);
                        var newObj = factory();
                        dict[keyObj] = newObj;
                        currentObject = newObj;
                        lastProperty = null;
                    }
                    else
                    {
                        return (null, null, null);
                    }
                }
                else
                {
                    return (null, null, null);
                }
            }
            else if (int.TryParse(currentSegment, out var index))
            {
                if (currentObject is IList list)
                {
                    if (index >= 0 && index < list.Count)
                    {
                        currentObject = list[index];
                        lastProperty = null; // We are inside an item, property context is reset
                    }
                    else { return (null, null, null); } // Index out of bounds
                }
                else { return (null, null, null); } // Not a list
            }
            else
            {
                var properties = GetPropertiesForType(currentObject.GetType());
#if NET9_0_OR_GREATER
                if (properties.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(currentSegment, out var propertyInfo))
#else
                if (properties.TryGetValue(currentSegment.ToString(), out var propertyInfo))
#endif
                {
                    lastProperty = propertyInfo;
                    var nextObject = GetAccessor(propertyInfo).Getter(currentObject);
                    if (nextObject is null && createMissing && propertyInfo.CanWrite)
                    {
                        // Instantiate missing intermediate POCOs automatically
                        var propType = propertyInfo.PropertyType;
                        if (propType.IsClass && propType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(propType))
                        {
                            var factory = ConstructorCache.GetOrAdd(propType, CreateConstructor);
                            nextObject = factory();
                            GetAccessor(propertyInfo).Setter(currentObject, nextObject);
                        }
                    }
                    currentObject = nextObject;
                }
                else { return (null, null, null); } // Property not found
            }
            
            currentSegment = enumerator.Current;
        }
        
        if (currentObject is null) return (null, null, null);

        // Now, resolve the last segment against the parent
        if (currentObject is IDictionary)
        {
            // The target is a key in a dictionary.
            // We return information about the dictionary's parent container and property.
            return (parentOfCurrent ?? root, lastProperty, new DictionaryKeyPathSegment(currentSegment.ToString()));
        }

        if (int.TryParse(currentSegment, out var lastIndex))
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
#if NET9_0_OR_GREATER
        if (parentProperties.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(currentSegment, out var finalProperty))
#else
        if (parentProperties.TryGetValue(currentSegment.ToString(), out var finalProperty))
#endif
        {
            return (currentObject, finalProperty, currentSegment.ToString());
        }
        
        return (null, null, null); // Could not resolve the final segment.
    }

    public static object? GetValue(object root, string jsonPath)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return null;
        }

        var (parent, property, finalSegment) = ResolvePath(root, jsonPath);

        if (parent is null || property is null)
        {
            return null;
        }

        var propertyValue = GetAccessor(property).Getter(parent);
        
        if (finalSegment is int index)
        {
            if (propertyValue is IList list && index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return null; // Index out of bounds or not a list
        }
        else if (finalSegment is DictionaryKeyPathSegment dictKey && propertyValue is IDictionary dict)
        {
            var keyType = GetDictionaryKeyType(property);
            var keyObj = ConvertValue(dictKey.Key, keyType);
            if (keyObj != null && dict.Contains(keyObj))
            {
                return dict[keyObj];
            }
            return null;
        }

        return propertyValue;
    }

    /// <summary>
    /// A generic implementation to retrieve property values while bypassing boxing completely.
    /// </summary>
    public static T GetValue<T>(object root, string jsonPath)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return default!;
        }

        var (parent, property, finalSegment) = ResolvePath(root, jsonPath);

        if (parent is null || property is null)
        {
            return default!;
        }

        if (finalSegment is int index)
        {
            var propertyValue = GetAccessor(property).Getter(parent);
            if (propertyValue is IList list && index >= 0 && index < list.Count)
            {
                var val = list[index];
                return ConvertTo<T>(val);
            }
            return default!;
        }
        else if (finalSegment is DictionaryKeyPathSegment dictKey)
        {
            var propertyValue = GetAccessor(property).Getter(parent);
            if (propertyValue is IDictionary dict)
            {
                var keyType = GetDictionaryKeyType(property);
                var keyObj = ConvertValue(dictKey.Key, keyType);
                if (keyObj != null && dict.Contains(keyObj))
                {
                    return ConvertTo<T>(dict[keyObj]);
                }
                return default!;
            }
        }

        // Delegate to the perfectly typed and precompiled property accessor
        return GenericPropertyAccessor<T>.GetGetter(property)(parent);
    }

    public static bool SetValue(object root, string jsonPath, object? value)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return false;
        }

        var (parent, property, finalSegment) = ResolvePath(root, jsonPath);

        if (parent is null || property is null)
        {
            return false;
        }

        var accessor = GetAccessor(property);

        if (finalSegment is int index)
        {
            if (accessor.Getter(parent) is IList list)
            {
                if (index < 0 || index >= list.Count) return false;

                var elementType = GetCollectionElementType(property);
                var convertedValue = ConvertValue(value, elementType);

                list[index] = convertedValue;
                return true;
            }
            return false;
        }
        else if (finalSegment is DictionaryKeyPathSegment dictKey && accessor.Getter(parent) is IDictionary dict)
        {
            var keyType = GetDictionaryKeyType(property);
            var valueType = GetDictionaryValueType(property);
            var keyObj = ConvertValue(dictKey.Key, keyType);
            if (keyObj != null)
            {
                dict[keyObj] = ConvertValue(value, valueType);
                return true;
            }
            return false;
        }

        if (!property.CanWrite)
        {
            return false;
        }

        var finalValue = ConvertValue(value, property.PropertyType);
        accessor.Setter(parent, finalValue);
        return true;
    }

    /// <summary>
    /// A generic implementation to set property values while bypassing boxing completely.
    /// </summary>
    public static bool SetValue<T>(object root, string jsonPath, T value)
    {
        if (root is null || string.IsNullOrEmpty(jsonPath))
        {
            return false;
        }

        var (parent, property, finalSegment) = ResolvePath(root, jsonPath);

        if (parent is null || property is null)
        {
            return false;
        }

        if (finalSegment is int index)
        {
            var accessor = GetAccessor(property);
            if (accessor.Getter(parent) is IList list)
            {
                if (index < 0 || index >= list.Count) return false;

                var elementType = GetCollectionElementType(property);
                var convertedValue = ConvertValue(value, elementType);

                list[index] = convertedValue;
                return true;
            }
            return false;
        }
        else if (finalSegment is DictionaryKeyPathSegment dictKey)
        {
            var accessor = GetAccessor(property);
            if (accessor.Getter(parent) is IDictionary dict)
            {
                var keyType = GetDictionaryKeyType(property);
                var valueType = GetDictionaryValueType(property);
                var keyObj = ConvertValue(dictKey.Key, keyType);
                if (keyObj != null)
                {
                    dict[keyObj] = ConvertValue(value, valueType);
                    return true;
                }
                return false;
            }
        }

        if (!property.CanWrite)
        {
            return false;
        }

        // Delegate to the perfectly typed and precompiled property accessor
        GenericPropertyAccessor<T>.GetSetter(property)(parent, value);
        return true;
    }

    /// <summary>
    /// Converts a value to the specified type using highly optimized compiled expressions.
    /// </summary>
    public static T ConvertTo<T>(object? value)
    {
        if (value is null) return default!;
        if (value is T tVal) return tVal;
        return GenericConverter<T>.Convert(value);
    }

    public static Type GetCollectionElementType(PropertyInfo property)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        return GetCollectionElementType(property.PropertyType);
    }
    
    public static Type GetCollectionElementType(Type collectionType)
    {
        if (collectionType is null)
        {
            throw new ArgumentNullException(nameof(collectionType));
        }

        return CollectionElementTypeCache.GetOrAdd(collectionType, type =>
        {
            var iEnumerableOfT = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (iEnumerableOfT != null)
            {
                return iEnumerableOfT.GetGenericArguments()[0];
            }

            return type.IsGenericType
                ? type.GetGenericArguments()[0]
                : type.GetElementType() ?? typeof(object);
        });
    }

    public static object InstantiateCollection(Type propertyType)
    {
        var elementType = GetCollectionElementType(propertyType);
        Type concreteType;

        if (propertyType.IsInterface)
        {
            var genericDef = propertyType.IsGenericType ? propertyType.GetGenericTypeDefinition() : null;
            if (genericDef == typeof(ISet<>) || genericDef == typeof(IReadOnlySet<>))
                concreteType = typeof(HashSet<>).MakeGenericType(elementType);
            else
                concreteType = typeof(List<>).MakeGenericType(elementType);
        }
        else if (propertyType.IsAbstract)
        {
            concreteType = typeof(List<>).MakeGenericType(elementType);
        }
        else
        {
            concreteType = propertyType;
        }

        var factory = ConstructorCache.GetOrAdd(concreteType, CreateConstructor);
        return factory();
    }

    public static void AddToCollection(object collection, object? item)
    {
        var type = collection.GetType();
        var adder = CollectionAdders.GetOrAdd(type, t =>
        {
            var elementType = GetCollectionElementType(t);
            var colParam = Expression.Parameter(typeof(object), "col");
            var itemParam = Expression.Parameter(typeof(object), "item");
            var castCol = Expression.Convert(colParam, t);
            
            var addMethod = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))?.GetMethod("Add") 
                            ?? (typeof(IList).IsAssignableFrom(t) ? typeof(IList).GetMethod("Add") : t.GetMethod("Add"));

            if (addMethod == null) return (_, _) => throw new InvalidOperationException($"Cannot find Add method on {t.Name}");

            var convertItemMethod = typeof(PocoPathHelper).GetMethod(nameof(ConvertValue), BindingFlags.Public | BindingFlags.Static)!;
            var elementTypeConst = Expression.Constant(elementType, typeof(Type));
            var convertedItem = Expression.Call(convertItemMethod, itemParam, elementTypeConst);
            var castItem = Expression.Convert(convertedItem, elementType);

            var call = Expression.Call(castCol, addMethod, castItem);
            var body = addMethod.ReturnType == typeof(void) ? (Expression)call : Expression.Block(typeof(void), call);

            return Expression.Lambda<Action<object, object?>>(body, colParam, itemParam).Compile();
        });
        adder(collection, item);
    }

    public static void RemoveFromCollection(object collection, object? item)
    {
        var type = collection.GetType();
        var remover = CollectionRemovers.GetOrAdd(type, t =>
        {
            var elementType = GetCollectionElementType(t);
            var colParam = Expression.Parameter(typeof(object), "col");
            var itemParam = Expression.Parameter(typeof(object), "item");
            var castCol = Expression.Convert(colParam, t);
            
            var removeMethod = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))?.GetMethod("Remove") 
                            ?? (typeof(IList).IsAssignableFrom(t) ? typeof(IList).GetMethod("Remove") : t.GetMethod("Remove"));

            if (removeMethod == null) return (_, _) => throw new InvalidOperationException($"Cannot find Remove method on {t.Name}");

            var convertItemMethod = typeof(PocoPathHelper).GetMethod(nameof(ConvertValue), BindingFlags.Public | BindingFlags.Static)!;
            var elementTypeConst = Expression.Constant(elementType, typeof(Type));
            var convertedItem = Expression.Call(convertItemMethod, itemParam, elementTypeConst);
            var castItem = Expression.Convert(convertedItem, elementType);

            var call = Expression.Call(castCol, removeMethod, castItem);
            var body = removeMethod.ReturnType == typeof(void) ? (Expression)call : Expression.Block(typeof(void), call);

            return Expression.Lambda<Action<object, object?>>(body, colParam, itemParam).Compile();
        });
        remover(collection, item);
    }

    public static void ClearCollection(object collection)
    {
        var type = collection.GetType();
        var clearer = CollectionClearers.GetOrAdd(type, t =>
        {
            var colParam = Expression.Parameter(typeof(object), "col");
            var castCol = Expression.Convert(colParam, t);
            
            var clearMethod = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))?.GetMethod("Clear") 
                            ?? (typeof(IList).IsAssignableFrom(t) ? typeof(IList).GetMethod("Clear") : t.GetMethod("Clear"));

            if (clearMethod == null) return (_) => throw new InvalidOperationException($"Cannot find Clear method on {t.Name}");

            var call = Expression.Call(castCol, clearMethod);
            var body = clearMethod.ReturnType == typeof(void) ? (Expression)call : Expression.Block(typeof(void), call);

            return Expression.Lambda<Action<object>>(body, colParam).Compile();
        });
        clearer(collection);
    }

    public static Type GetDictionaryKeyType(Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return DictionaryKeyTypeCache.GetOrAdd(type, t =>
        {
            // Check if the type is or implements IDictionary<,>
            var genericDictionaryInterface = (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                ? t
                : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (genericDictionaryInterface is not null)
            {
                return genericDictionaryInterface.GetGenericArguments()[0];
            }
            
            // Fallback for non-generic IDictionary, as we cannot determine the key type.
            return typeof(object);
        });
    }

    public static Type GetDictionaryValueType(Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return DictionaryValueTypeCache.GetOrAdd(type, t =>
        {
            // Check if the type is or implements IDictionary<,>
            var genericDictionaryInterface = (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                ? t
                : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (genericDictionaryInterface is not null)
            {
                return genericDictionaryInterface.GetGenericArguments()[1];
            }

            // Fallback for non-generic IDictionary, as we cannot determine the value type.
            return typeof(object);
        });
    }

    public static Type GetDictionaryKeyType(PropertyInfo property)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }
        return GetDictionaryKeyType(property.PropertyType);
    }

    public static Type GetDictionaryValueType(PropertyInfo property)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }
        return GetDictionaryValueType(property.PropertyType);
    }

    public static object? ConvertValue(object? value, Type targetType)
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
                return value; // Silently fallback on invalid object conversion
            }
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

                // Replaced Activator.CreateInstance with compiled cached constructor lambda
                var factory = KvpConstructorCache.GetOrAdd(underlyingType, CreateKvpConstructor);
                return factory(key, val);
            }

            if (!underlyingType.IsPrimitive && underlyingType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(underlyingType))
            {
                // Replaced Activator.CreateInstance with compiled cached constructor lambda
                var factory = ConstructorCache.GetOrAdd(underlyingType, CreateConstructor);
                var instance = factory();
                
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
                        
                        // Utilize the fast setter instead of reflection
                        var accessor = GetAccessor(propInfo);
                        accessor.Setter(instance, propValue);
                    }
                }
                return instance;
            }
        }

        // Fast path for Guids to avoid IConvertible logic
        if (underlyingType == typeof(Guid) && value is string guidString)
        {
            return Guid.TryParse(guidString, out var parsedGuid) ? parsedGuid : value;
        }

        try
        {
            // Using IConvertible directly is fully equivalent to Convert.ChangeType overhead and avoids analyzers
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
    
    internal static CachedPropertyMetadata[] GetCachedProperties(Type type)
    {
        return MetadataPropertyCache.GetOrAdd(type, static t =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .Select(p => 
                {
                    var jsonPropertyName = SerializerOptions.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name;
                    return new CachedPropertyMetadata(p, jsonPropertyName);
                })]);
    }

    internal static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    internal static ParseResult ParseExpression<T, TProp>(Expression<Func<T, TProp>> expression)
    {
        var current = expression.Body;
        
        // Unwrap potential boxing
        if (current is UnaryExpression unary)
        {
            current = unary.Operand;
        }

        PropertyInfo? targetProperty = null;
        if (current is MemberExpression initialMe && initialMe.Member is PropertyInfo pi)
        {
            targetProperty = pi;
        }

        if (targetProperty is null)
        {
            throw new ArgumentException(
                "Expression must end in a property access. " +
                "If you are trying to replace an entire collection element, target the collection property instead " +
                "and use a collection-specific intent (e.g., SetIndexIntent or MapSetIntent).", nameof(expression));
        }

        var jsonPath = BuildJsonPath(current);
        return new ParseResult(jsonPath, targetProperty);
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

    private static Dictionary<string, PropertyInfo> GetPropertiesForType(Type type)
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

    private static Func<object> CreateConstructor(Type type)
    {
        if (type.IsValueType)
        {
            var newExp = Expression.New(type);
            var castExp = Expression.Convert(newExp, typeof(object));
            return Expression.Lambda<Func<object>>(castExp).Compile();
        }

        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor is null)
        {
            return () => throw new InvalidOperationException($"No parameterless constructor found for type '{type.Name}'.");
        }

        var newRefExp = Expression.New(ctor);
        var castRefExp = Expression.Convert(newRefExp, typeof(object));
        return Expression.Lambda<Func<object>>(castRefExp).Compile();
    }

    private static Func<object?, object?, object> CreateKvpConstructor(Type type)
    {
        var keyType = type.GetGenericArguments()[0];
        var valueType = type.GetGenericArguments()[1];
        var ctor = type.GetConstructor([keyType, valueType]);
        
        var keyParam = Expression.Parameter(typeof(object), "key");
        var valParam = Expression.Parameter(typeof(object), "val");

        var castKey = GetSafeConvert(keyParam, keyType);
        var castVal = GetSafeConvert(valParam, valueType);

        var newExp = Expression.New(ctor!, castKey, castVal);
        var castResult = Expression.Convert(newExp, typeof(object));

        return Expression.Lambda<Func<object?, object?, object>>(castResult, keyParam, valParam).Compile();
    }

    private static Expression GetSafeConvert(Expression param, Type targetType)
    {
        if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
        {
            // Safeguard against passing null objects into value-type constructors, falling back to default(T)
            var isNull = Expression.Equal(param, Expression.Constant(null, typeof(object)));
            return Expression.Condition(
                isNull,
                Expression.Default(targetType),
                Expression.Convert(param, targetType));
        }
        return Expression.Convert(param, targetType);
    }
}