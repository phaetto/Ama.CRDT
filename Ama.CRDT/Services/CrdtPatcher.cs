namespace Ama.CRDT.Services;

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

/// <inheritdoc/>
public sealed class CrdtPatcher(ICrdtStrategyProvider strategyProvider, ICrdtTimestampProvider timestampProvider) : ICrdtPatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <inheritdoc/>
    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, T changed) where T : class
    {
        var changeTimestamp = timestampProvider.Now();
        return GeneratePatch(from, changed, changeTimestamp);
    }

    /// <inheritdoc/>
    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(from.Metadata);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(changeTimestamp);

        var operations = new List<CrdtOperation>();
        var context = new DifferentiateObjectContext(
            "$",
            typeof(T),
            from.Data,
            changed,
            from.Data,
            changed,
            from.Metadata,
            operations,
            changeTimestamp
        );
        DifferentiateObject(context);

        return new CrdtPatch(operations);
    }

    /// <inheritdoc/>
    public void DifferentiateObject(DifferentiateObjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (path, type, fromObj, toObj, fromRoot, toRoot, fromMeta, operations, changeTimestamp) = context;

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(fromMeta);
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
            var strategyContext = new GeneratePatchContext(this, operations, currentPath, property, fromValue, toValue, fromRoot, toRoot, fromMeta, changeTimestamp);
            
            strategy.GeneratePatch(strategyContext);
        }
    }
    
    internal static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }
}