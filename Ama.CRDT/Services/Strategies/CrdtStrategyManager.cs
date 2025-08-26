namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

/// <inheritdoc/>
public sealed class CrdtStrategyManager : ICrdtStrategyManager
{
    private readonly IReadOnlyDictionary<Type, ICrdtStrategy> strategies;
    private readonly ICrdtStrategy defaultStrategy;
    private readonly ICrdtStrategy defaultArrayStrategy;
    private readonly ICrdtStrategy defaultDictionaryStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtStrategyManager"/> class.
    /// </summary>
    /// <param name="strategies">An enumerable of all registered <see cref="ICrdtStrategy"/> instances.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="strategies"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a required default strategy (like <see cref="LwwStrategy"/>) is not registered.</exception>
    public CrdtStrategyManager(IEnumerable<ICrdtStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        this.strategies = strategies
            .GroupBy(s => s.GetType())
            .ToDictionary(g => g.Key, g => g.First());
        
        defaultStrategy = this.strategies.Values.OfType<LwwStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The default '{nameof(LwwStrategy)}' is not registered in the DI container.");
        
        defaultArrayStrategy = this.strategies.Values.OfType<ArrayLcsStrategy>().FirstOrDefault() 
            ?? this.strategies.Values.OfType<SortedSetStrategy>().FirstOrDefault() 
            ?? defaultStrategy;
        
        defaultDictionaryStrategy = this.strategies.Values.OfType<OrMapStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The default '{nameof(OrMapStrategy)}' for dictionaries is not registered in the DI container.");
    }

    /// <inheritdoc/>
    public ICrdtStrategy GetStrategy([DisallowNull] PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var attribute = propertyInfo.GetCustomAttribute<CrdtStrategyAttribute>();
        if (attribute is not null && strategies.TryGetValue(attribute.StrategyType, out var strategy))
        {
            return strategy;
        }
        
        var propertyType = propertyInfo.PropertyType;
        if (propertyType != typeof(string) && typeof(IDictionary).IsAssignableFrom(propertyType))
        {
            return defaultDictionaryStrategy;
        }

        if (propertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyType))
        {
            return defaultArrayStrategy;
        }

        return defaultStrategy;
    }
    
    /// <inheritdoc/>
    public ICrdtStrategy GetStrategy(CrdtOperation operation, [DisallowNull] object root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var (_, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        
        if (property is null)
        {
            return operation.JsonPath.Contains('[') ? defaultArrayStrategy : defaultStrategy;
        }

        return GetStrategy(property);
    }
}