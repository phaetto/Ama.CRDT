namespace Modern.CRDT.Services.Strategies;

using Modern.CRDT.Attributes;
using Modern.CRDT.Models;
using Modern.CRDT.Services.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Implements the strategy resolution logic. It inspects property attributes
/// to find the correct strategy from a collection of registered strategies.
/// </summary>
public sealed class CrdtStrategyManager : ICrdtStrategyManager
{
    private readonly IReadOnlyDictionary<Type, ICrdtStrategy> strategies;
    private readonly ICrdtStrategy defaultStrategy;
    private readonly ICrdtStrategy defaultArrayStrategy;

    public CrdtStrategyManager(IEnumerable<ICrdtStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        this.strategies = strategies.ToDictionary(s => s.GetType());
        
        defaultStrategy = this.strategies.Values.OfType<LwwStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The default '{nameof(LwwStrategy)}' is not registered in the DI container.");
        
        defaultArrayStrategy = this.strategies.Values.OfType<ArrayLcsStrategy>().FirstOrDefault() ?? defaultStrategy;
    }

    /// <inheritdoc/>
    public ICrdtStrategy GetStrategy(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var attribute = propertyInfo.GetCustomAttribute<CrdtStrategyAttribute>();
        if (attribute is not null && strategies.TryGetValue(attribute.StrategyType, out var strategy))
        {
            return strategy;
        }
        
        var propertyType = propertyInfo.PropertyType;
        if (propertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyType))
        {
            return defaultArrayStrategy;
        }

        return defaultStrategy;
    }
    
    public ICrdtStrategy GetStrategy(CrdtOperation operation, object root)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(root);

        var (_, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        
        if (property is null)
        {
            // If the path leads to a property that doesn't exist (e.g., adding a new property),
            // we can't reflect on its attributes. We must infer the strategy.
            // For now, we assume LWW is a safe default for non-array paths.
            return operation.JsonPath.Contains('[') ? defaultArrayStrategy : defaultStrategy;
        }

        return GetStrategy(property);
    }
}