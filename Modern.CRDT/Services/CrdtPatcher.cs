namespace Modern.CRDT.Services;

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;

public sealed class CrdtPatcher(ICrdtStrategyManager strategyManager) : ICrdtPatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var operations = new List<CrdtOperation>();
        DifferentiateObject("$", typeof(T), from.Data, from.Metadata, to.Data, to.Metadata, operations);

        return new CrdtPatch(operations);
    }
    
    public void DifferentiateObject(string path, Type type, object? fromObj, CrdtMetadata fromMeta, object? toObj, CrdtMetadata toMeta, List<CrdtOperation> operations)
    {
        if (fromObj is null && toObj is null)
        {
            return;
        }

        var properties = PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .ToArray());

        foreach (var property in properties)
        {
            var jsonPropertyName = SerializerOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            var currentPath = path == "$" ? $"$.{jsonPropertyName}" : $"{path}.{jsonPropertyName}";

            var fromValue = fromObj is not null ? property.GetValue(fromObj) : null;
            var toValue = toObj is not null ? property.GetValue(toObj) : null;

            if (Equals(fromValue, toValue))
            {
                continue;
            }

            var strategy = strategyManager.GetStrategy(property);
            
            strategy.GeneratePatch(this, operations, currentPath, property, fromValue, toValue, fromMeta, toMeta);
        }
    }
    
    internal static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }
}