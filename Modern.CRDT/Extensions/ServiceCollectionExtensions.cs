using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Modern.CRDT.UnitTests")]

namespace Modern.CRDT.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsonCrdt(this IServiceCollection services)
    {
        services.TryAddSingleton<IJsonCrdtPatcher, JsonCrdtPatcher>();
        services.TryAddSingleton<IJsonCrdtApplicator, JsonCrdtApplicator>();
        services.TryAddSingleton<IJsonCrdtService, JsonCrdtService>();

        // Register the strategy manager
        services.TryAddSingleton<ICrdtStrategyManager, CrdtStrategyManager>();

        // Register concrete strategies as singletons so the manager can resolve them.
        services.TryAddSingleton<LwwStrategy>();
        services.TryAddSingleton<CounterStrategy>();
        
        // This allows the CrdtStrategyManager to get all registered strategies
        services.AddSingleton<ICrdtStrategy, LwwStrategy>(sp => sp.GetRequiredService<LwwStrategy>());
        services.AddSingleton<ICrdtStrategy, CounterStrategy>(sp => sp.GetRequiredService<CounterStrategy>());

        return services;
    }
}