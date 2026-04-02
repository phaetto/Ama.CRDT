namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Strategies;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

/// <inheritdoc/>
internal sealed class CrdtStrategyProvider : ICrdtStrategyProvider
{
    private readonly IReadOnlyDictionary<Type, ICrdtStrategy> strategies;
    private readonly ICrdtModelRegistry registry;
    private readonly IEnumerable<CrdtContext> aotContexts;
    private readonly ICrdtStrategy defaultStrategy;
    private readonly ICrdtStrategy defaultArrayStrategy;
    private readonly ICrdtStrategy defaultDictionaryStrategy;

    private readonly ConcurrentDictionary<CrdtPropertyKey, ICrdtStrategy> strategyCache = new();
    private readonly ConcurrentDictionary<CrdtPropertyKey, ICrdtStrategy> baseStrategyCache = new();
    private readonly ConcurrentDictionary<CrdtPropertyKey, IReadOnlyList<Type>> decoratorChainCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtStrategyProvider"/> class.
    /// </summary>
    /// <param name="strategies">An enumerable of all registered <see cref="ICrdtStrategy"/> instances.</param>
    /// <param name="registry">The configuration registry that defines runtime strategy bindings.</param>
    /// <param name="aotContexts">The collection of AOT contexts used to resolve type information.</param>
    /// <exception cref="ArgumentNullException">Thrown if parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a required default strategy (like <see cref="LwwStrategy"/>) is not registered.</exception>
    public CrdtStrategyProvider(
        IEnumerable<ICrdtStrategy> strategies, 
        ICrdtModelRegistry registry,
        IEnumerable<CrdtContext> aotContexts)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(aotContexts);

        this.registry = registry;
        this.aotContexts = aotContexts;
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
    public ICrdtStrategy GetStrategy([DisallowNull] Type declaringType, [DisallowNull] CrdtPropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var key = new CrdtPropertyKey(declaringType, propertyInfo.Name);

        return strategyCache.GetOrAdd(key, k =>
        {
            var decorators = GetDecorators(k, propertyInfo);
            if (decorators.Count > 0 && strategies.TryGetValue(decorators[0], out var decoratorStrategy))
            {
                return decoratorStrategy;
            }

            return GetBaseStrategy(declaringType, propertyInfo);
        });
    }

    /// <inheritdoc/>
    public ICrdtStrategy GetInnerStrategy([DisallowNull] Type declaringType, [DisallowNull] CrdtPropertyInfo propertyInfo, [DisallowNull] Type currentDecoratorType)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(propertyInfo);
        ArgumentNullException.ThrowIfNull(currentDecoratorType);

        var key = new CrdtPropertyKey(declaringType, propertyInfo.Name);
        var decorators = GetDecorators(key, propertyInfo);
        
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

        return GetBaseStrategy(declaringType, propertyInfo);
    }
    
    /// <inheritdoc/>
    public ICrdtStrategy GetBaseStrategy([DisallowNull] Type declaringType, [DisallowNull] CrdtPropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var key = new CrdtPropertyKey(declaringType, propertyInfo.Name);

        return baseStrategyCache.GetOrAdd(key, k =>
        {
            // 1. Check Fluent API Registry
            if (registry.TryGetStrategy(k, out var configuredStrategyType) && 
                strategies.TryGetValue(configuredStrategyType, out var configuredStrategy))
            {
                return configuredStrategy;
            }

            // 2. Check AOT precalculated attributes extracted during generation 
            if (propertyInfo.StrategyType is not null && strategies.TryGetValue(propertyInfo.StrategyType, out var strategy))
            {
                return strategy;
            }
            
            // 3. Fallback to defaults based on property type
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
        });
    }
    
    /// <inheritdoc/>
    public ICrdtStrategy GetStrategy(CrdtOperation operation, [DisallowNull] object root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var resolution = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        
        if (resolution.Property is null || resolution.Parent is null)
        {
            return operation.JsonPath.Contains('[') ? defaultArrayStrategy : defaultStrategy;
        }

        return GetStrategy(resolution.Parent.GetType(), resolution.Property);
    }

    private IReadOnlyList<Type> GetDecorators(CrdtPropertyKey key, CrdtPropertyInfo propertyInfo)
    {
        return decoratorChainCache.GetOrAdd(key, k => 
        {
            // 1. Check Fluent API Registry
            if (registry.TryGetDecorators(k, out var configuredDecorators))
            {
                // Ensure consistent resolution order
                return configuredDecorators
                    .OrderBy(d => d.Name, StringComparer.Ordinal)
                    .ToList();
            }

            // 2. Fallback to AOT precalculated list compiled by the generator 
            return propertyInfo.DecoratorTypes;
        });
    }
}