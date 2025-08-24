using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;

[assembly: InternalsVisibleTo("Modern.CRDT.UnitTests")]
[assembly: InternalsVisibleTo("Modern.CRDT.Benchmarks")]

namespace Modern.CRDT.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsonCrdt(this IServiceCollection services, Action<CrdtOptions> configureOptions)
    {
        if (configureOptions is null)
        {
            throw new ArgumentNullException(nameof(configureOptions), "CRDT options configuration cannot be null.");
        }
        
        services.AddOptions<CrdtOptions>()
            .Configure(configureOptions)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ReplicaId), "CrdtOptions.ReplicaId cannot be null or empty.");
        
        services.TryAddSingleton<ICrdtPatcherFactory, CrdtPatcherFactory>();
        
        // The default IJsonCrdtPatcher is a singleton created via the factory with the globally configured ReplicaId.
        // This maintains the original behavior for single-replica applications.
        services.TryAddSingleton<ICrdtPatcher>(sp =>
        {
            var factory = sp.GetRequiredService<ICrdtPatcherFactory>();
            var options = sp.GetRequiredService<IOptions<CrdtOptions>>();
            return factory.Create(options.Value.ReplicaId);
        });

        services.TryAddSingleton<ICrdtApplicator, CrdtApplicator>();
        services.TryAddSingleton<ICrdtService, CrdtService>();

        // Register the metadata manager
        services.TryAddSingleton<ICrdtMetadataManager, CrdtMetadataManager>();

        // Register the strategy manager
        services.TryAddSingleton<ICrdtStrategyManager, CrdtStrategyManager>();

        // Register the comparer provider for ArrayLcsStrategy
        services.TryAddSingleton<IElementComparerProvider, ElementComparerProvider>();
        
        // Register the default timestamp provider
        services.TryAddSingleton<ICrdtTimestampProvider, EpochTimestampProvider>();
        
        // Register concrete strategies as singletons so the manager can resolve them for the *default* replica.
        // The factory will create new instances for other replicas.
        services.TryAddSingleton<LwwStrategy>();
        services.TryAddSingleton<CounterStrategy>();
        services.TryAddSingleton<ArrayLcsStrategy>();

        // This allows the CrdtStrategyManager to get all registered strategies
        services.AddSingleton<ICrdtStrategy, LwwStrategy>(sp => sp.GetRequiredService<LwwStrategy>());
        services.AddSingleton<ICrdtStrategy, CounterStrategy>(sp => sp.GetRequiredService<CounterStrategy>());
        services.AddSingleton<ICrdtStrategy, ArrayLcsStrategy>(sp => sp.GetRequiredService<ArrayLcsStrategy>());

        return services;
    }

    /// <summary>
    /// Registers a custom JSON node comparer for the Array LCS strategy.
    /// </summary>
    /// <typeparam name="TComparer">The type of the comparer to register. Must implement <see cref="IElementComparer"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJsonCrdtComparer<TComparer>(this IServiceCollection services)
        where TComparer : class, IElementComparer
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IElementComparer, TComparer>());
        return services;
    }
    
    /// <summary>
    /// Registers a custom timestamp provider. This will replace the default <see cref="EpochTimestampProvider"/>.
    /// </summary>
    /// <typeparam name="TProvider">The type of the timestamp provider to register. Must implement <see cref="ICrdtTimestampProvider"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddJsonCrdtTimestampProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICrdtTimestampProvider
    {
        services.AddSingleton<ICrdtTimestampProvider, TProvider>();
        return services;
    }
}