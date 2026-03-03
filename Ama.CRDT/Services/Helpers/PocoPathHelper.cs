namespace Ama.CRDT.Services.Helpers;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// A utility class containing helper methods for parsing JSON paths and resolving them against POCOs using reflection.
/// </summary>
internal static class PocoPathHelper
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, PropertyAccessor> AccessorCache = new();
    
    // Type resolution caches to avoid repetitive LINQ and reflection overhead
    private static readonly ConcurrentDictionary<Type, Type> CollectionElementTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Type> DictionaryKeyTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Type> DictionaryValueTypeCache = new();

    // Expression compilation caches to replace Activator.CreateInstance
    private static readonly ConcurrentDictionary<Type, Func<object>> ConstructorCache = new();
    private static readonly ConcurrentDictionary<Type, Func<object?, object?, object>> KvpConstructorCache = new();

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
    /// to cast or convert between a runtime type and T, completely eliminating Convert.ChangeType.
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

            var underlyingTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var underlyingSource = Nullable.GetUnderlyingType(sourceType) ?? sourceType;

            // Enums convert natively
            if (underlyingTarget.IsEnum || underlyingSource.IsEnum)
            {
                return Expression.Convert(source, targetType);
            }

            // If the source is an object/interface, we can't emit a direct unboxing conversion
            // without knowing the exact boxed type, so we safely fall back to ChangeType.
            if (sourceType == typeof(object) || sourceType == typeof(ValueType) || sourceType.IsInterface)
            {
                return BuildChangeTypeCall(source, targetType);
            }

            try
            {
                // Let the Expression engine build the best direct conversion (e.g. IL conv.r8 for int -> decimal)
                return Expression.Convert(source, targetType);
            }
            catch (InvalidOperationException)
            {
                // Fallback for types that lack an implicit/explicit cast operator (like string -> int)
                return BuildChangeTypeCall(source, targetType);
            }
        }

        internal static Expression BuildChangeTypeCall(Expression source, Type targetType)
        {
            var convertMethod = typeof(System.Convert).GetMethod(nameof(System.Convert.ChangeType), [typeof(object), typeof(Type), typeof(IFormatProvider)]);
            var changeTypeCall = Expression.Call(convertMethod!, 
                Expression.Convert(source, typeof(object)), 
                Expression.Constant(targetType), 
                Expression.Constant(CultureInfo.InvariantCulture));
            return Expression.Convert(changeTypeCall, targetType);
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

            if (int.TryParse(currentSegment, out var index))
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

        return CollectionElementTypeCache.GetOrAdd(property.PropertyType, type =>
        {
            return type.IsGenericType
                ? type.GetGenericArguments()[0]
                : type.GetElementType() ?? typeof(object);
        });
    }

    public static Type GetDictionaryKeyType(PropertyInfo property)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        return DictionaryKeyTypeCache.GetOrAdd(property.PropertyType, type =>
        {
            // Check if the type is or implements IDictionary<,>
            var genericDictionaryInterface = (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                ? type
                : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (genericDictionaryInterface is not null)
            {
                return genericDictionaryInterface.GetGenericArguments()[0];
            }
            
            // Fallback for non-generic IDictionary, as we cannot determine the key type.
            return typeof(object);
        });
    }

    public static Type GetDictionaryValueType(PropertyInfo property)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        return DictionaryValueTypeCache.GetOrAdd(property.PropertyType, type =>
        {
            // Check if the type is or implements IDictionary<,>
            var genericDictionaryInterface = (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                ? type
                : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (genericDictionaryInterface is not null)
            {
                return genericDictionaryInterface.GetGenericArguments()[1];
            }

            // Fallback for non-generic IDictionary, as we cannot determine the value type.
            return typeof(object);
        });
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

        // Fast path for Guids to avoid Convert.ChangeType exception behavior
        if (underlyingType == typeof(Guid) && value is string guidString)
        {
            return Guid.TryParse(guidString, out var parsedGuid) ? parsedGuid : value;
        }

        try
        {
            // Using IConvertible directly is much faster than full Convert.ChangeType overhead
            if (value is IConvertible convertible)
            {
                return convertible.ToType(underlyingType, CultureInfo.InvariantCulture);
            }
            
            return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return value;
        }
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