namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Strategies;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

/// <inheritdoc/>
internal sealed class CrdtStrategyProvider : ICrdtStrategyProvider
{
    private readonly IReadOnlyDictionary<Type, ICrdtStrategy> strategies;
    private readonly ICrdtStrategy defaultStrategy;
    private readonly ICrdtStrategy defaultArrayStrategy;
    private readonly ICrdtStrategy defaultDictionaryStrategy;

    private readonly ConcurrentDictionary<PropertyInfo, ICrdtStrategy> strategyCache = new();
    private readonly ConcurrentDictionary<PropertyInfo, ICrdtStrategy> baseStrategyCache = new();
    private readonly ConcurrentDictionary<PropertyInfo, IReadOnlyList<Type>> decoratorChainCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtStrategyProvider"/> class.
    /// </summary>
    /// <param name="strategies">An enumerable of all registered <see cref="ICrdtStrategy"/> instances.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="strategies"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a required default strategy (like <see cref="LwwStrategy"/>) is not registered.</exception>
    public CrdtStrategyProvider(IEnumerable<ICrdtStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        this.strategies = strategies
            .GroupBy(s => s.GetType())
            .ToDictionary(g => g.Key, g => g.First());
        
        defaultStrategy = this.strategies.Values.OfType<LwwStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The default '{nameof(LwwStrategy)}' is not registered in the DI container.");
        
        defaultArrayStrategy = this.strategies.Values.OfType<ArrayLcsStrategy>().FirstOrDefault() 
            ?? defaultStrategy;
        
        defaultDictionaryStrategy = this.strategies.Values.OfType<LwwMapStrategy>().FirstOrDefault()
            ?? defaultStrategy;
    }

    /// <inheritdoc/>
    public ICrdtStrategy GetStrategy([DisallowNull] PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        return strategyCache.GetOrAdd(propertyInfo, pi =>
        {
            var decorators = GetDecorators(pi);
            if (decorators.Count > 0 && strategies.TryGetValue(decorators[0], out var decoratorStrategy))
            {
                return decoratorStrategy;
            }

            return GetBaseStrategy(pi);
        });
    }

    /// <inheritdoc/>
    public ICrdtStrategy GetInnerStrategy([DisallowNull] PropertyInfo propertyInfo, [DisallowNull] Type currentDecoratorType)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);
        ArgumentNullException.ThrowIfNull(currentDecoratorType);

        var decorators = GetDecorators(propertyInfo);
        
        for (var i = 0; i < decorators.Count; i++)
        {
            if (decorators[i] == currentDecoratorType)
            {
                if (i + 1 < decorators.Count && strategies.TryGetValue(decorators[i + 1], out var nextStrategy))
                {
                    return nextStrategy;
                }
                break;
            }
        }

        return GetBaseStrategy(propertyInfo);
    }
    
    /// <inheritdoc/>
    public ICrdtStrategy GetBaseStrategy([DisallowNull] PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        return baseStrategyCache.GetOrAdd(propertyInfo, pi =>
        {
            var attribute = pi.GetCustomAttribute<CrdtStrategyAttribute>();
            if (attribute is not null && strategies.TryGetValue(attribute.StrategyType, out var strategy))
            {
                return strategy;
            }
            
            var propertyType = pi.PropertyType;
            if (propertyType != typeof(string) && typeof(IDictionary).IsAssignableFrom(propertyType))
            {
                return defaultDictionaryStrategy;
            }

            if (propertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyType))
            {
                return defaultArrayStrategy;
            }

            return defaultStrategy;
        });
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

    private IReadOnlyList<Type> GetDecorators(PropertyInfo propertyInfo)
    {
        return decoratorChainCache.GetOrAdd(propertyInfo, pi => 
            pi.GetCustomAttributes<CrdtStrategyDecoratorAttribute>()
              .OrderBy(a => a.GetType().Name, StringComparer.Ordinal)
              .Select(a => a.StrategyType)
              .ToList());
    }
}