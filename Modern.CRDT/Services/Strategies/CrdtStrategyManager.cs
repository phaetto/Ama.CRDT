namespace Modern.CRDT.Services.Strategies;

using Modern.CRDT.Attributes;
using System;
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

    public CrdtStrategyManager(IEnumerable<ICrdtStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        this.strategies = strategies.ToDictionary(s => s.GetType());
        this.defaultStrategy = this.strategies.Values.OfType<LwwStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The default '{nameof(LwwStrategy)}' is not registered in the DI container.");
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

        return defaultStrategy;
    }
}