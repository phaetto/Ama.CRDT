namespace Modern.CRDT.Services.Strategies;

using Microsoft.Extensions.DependencyInjection;
using Modern.CRDT.Attributes;
using System;
using System.Reflection;

/// <summary>
/// Manages the resolution of CRDT strategies based on property attributes.
/// It uses a DI container to instantiate strategies, allowing them to have dependencies.
/// </summary>
public sealed class CrdtStrategyManager(IServiceProvider serviceProvider) : ICrdtStrategyManager
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc/>
    public ICrdtStrategy GetStrategy(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var attribute = propertyInfo.GetCustomAttribute<CrdtStrategyAttribute>();

        var strategyType = attribute?.StrategyType ?? typeof(LwwStrategy);

        var strategy = serviceProvider.GetService(strategyType) as ICrdtStrategy;

        return strategy ?? throw new InvalidOperationException($"Strategy of type '{strategyType.Name}' is not registered in the DI container. Make sure to register it using `services.AddTransient<LwwStrategy>();` or similar.");
    }
}