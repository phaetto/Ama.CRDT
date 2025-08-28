namespace Ama.CRDT.Services;

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;

/// <inheritdoc/>
public sealed class CrdtPatcher(ICrdtStrategyProvider strategyProvider) : ICrdtPatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <inheritdoc/>
    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class
    {
        ArgumentNullException.ThrowIfNull(from.Metadata, nameof(from));
        ArgumentNullException.ThrowIfNull(to.Metadata, nameof(to));

        var operations = new List<CrdtOperation>();
        DifferentiateObject("$", typeof(T), from.Data, from.Metadata, to.Data, to.Metadata, operations, from.Data, to.Data);

        return new CrdtPatch(operations);
    }
    
    /// <inheritdoc/>
    public void DifferentiateObject(string path, [DisallowNull] Type type, object? fromObj, [DisallowNull] CrdtMetadata fromMeta, object? toObj, [DisallowNull] CrdtMetadata toMeta, [DisallowNull] List<CrdtOperation> operations, object? fromRoot, object? toRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(fromMeta);
        ArgumentNullException.ThrowIfNull(toMeta);
        ArgumentNullException.ThrowIfNull(operations);
        
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

            var strategy = strategyProvider.GetStrategy(property);
            
            strategy.GeneratePatch(this, operations, currentPath, property, fromValue, toValue, fromRoot, toRoot, fromMeta, toMeta);
        }
    }
    
    internal static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }
}