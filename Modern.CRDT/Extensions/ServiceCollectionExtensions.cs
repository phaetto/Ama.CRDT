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
        services.TryAddScoped<ICrdtStrategyManager, CrdtStrategyManager>();

        // Register concrete strategies as transient so the manager can resolve them.
        services.AddTransient<LwwStrategy>();

        return services;
    }
}