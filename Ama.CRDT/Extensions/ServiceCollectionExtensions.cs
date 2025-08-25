using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("Ama.CRDT.UnitTests")]
[assembly: InternalsVisibleTo("Ama.CRDT.Benchmarks")]

namespace Ama.CRDT.Extensions;

/// <summary>
/// Provides extension methods for setting up CRDT services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core CRDT services to the specified <see cref="IServiceCollection"/>. This includes the default strategies,
    /// patcher, applicator, and supporting services required for the library to function.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">An <see cref="Action{CrdtOptions}"/> to configure the provided <see cref="CrdtOptions"/>, typically used to set the default replica ID.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// builder.Services.AddCrdt(options =>
    /// {
    ///     // This replica ID is used for the default ICrdtService.
    ///     options.ReplicaId = "server-default";
    /// });
    ///
    /// var app = builder.Build();
    ///
    /// // You can now resolve ICrdtService, ICrdtPatcherFactory, etc.
    /// var crdtService = app.Services.GetRequiredService<ICrdtService>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdt(this IServiceCollection services, [DisallowNull] Action<CrdtOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);
        
        services.AddOptions<CrdtOptions>()
            .Configure(configureOptions)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ReplicaId), "CrdtOptions.ReplicaId cannot be null or empty.");
        
        services.TryAddSingleton<ICrdtPatcherFactory, CrdtPatcherFactory>();
        
        services.TryAddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<ICrdtPatcherFactory>();
            var options = sp.GetRequiredService<IOptions<CrdtOptions>>();
            return factory.Create(options.Value.ReplicaId);
        });

        services.TryAddSingleton<ICrdtApplicator, CrdtApplicator>();
        services.TryAddSingleton<ICrdtService, CrdtService>();

        services.AddTransient<ICrdtPatchBuilder, CrdtPatchBuilder>();

        services.TryAddSingleton<ICrdtMetadataManager, CrdtMetadataManager>();
        services.TryAddSingleton<ICrdtStrategyManager, CrdtStrategyManager>();
        services.TryAddSingleton<IElementComparerProvider, ElementComparerProvider>();
        services.TryAddSingleton<ICrdtTimestampProvider, EpochTimestampProvider>();
        
        services.TryAddSingleton<LwwStrategy>();
        services.TryAddSingleton<CounterStrategy>();
        services.TryAddSingleton<SortedSetStrategy>();
        services.TryAddSingleton<ArrayLcsStrategy>();
        services.TryAddSingleton<GCounterStrategy>();
        services.TryAddSingleton<BoundedCounterStrategy>();
        services.TryAddSingleton<MaxWinsStrategy>();
        services.TryAddSingleton<MinWinsStrategy>();
        services.TryAddSingleton<AverageRegisterStrategy>();

        services.AddSingleton<ICrdtStrategy, LwwStrategy>(sp => sp.GetRequiredService<LwwStrategy>());
        services.AddSingleton<ICrdtStrategy, CounterStrategy>(sp => sp.GetRequiredService<CounterStrategy>());
        services.AddSingleton<ICrdtStrategy, SortedSetStrategy>(sp => sp.GetRequiredService<SortedSetStrategy>());
        services.AddSingleton<ICrdtStrategy, ArrayLcsStrategy>(sp => sp.GetRequiredService<ArrayLcsStrategy>());
        services.AddSingleton<ICrdtStrategy, GCounterStrategy>(sp => sp.GetRequiredService<GCounterStrategy>());
        services.AddSingleton<ICrdtStrategy, BoundedCounterStrategy>(sp => sp.GetRequiredService<BoundedCounterStrategy>());
        services.AddSingleton<ICrdtStrategy, MaxWinsStrategy>(sp => sp.GetRequiredService<MaxWinsStrategy>());
        services.AddSingleton<ICrdtStrategy, MinWinsStrategy>(sp => sp.GetRequiredService<MinWinsStrategy>());
        services.AddSingleton<ICrdtStrategy, AverageRegisterStrategy>(sp => sp.GetRequiredService<AverageRegisterStrategy>());

        return services;
    }

    /// <summary>
    /// Registers a custom POCO comparer for the Array LCS strategy.
    /// </summary>
    /// <typeparam name="TComparer">The type of the comparer to register. Must implement <see cref="IElementComparer"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCrdtComparer<TComparer>(this IServiceCollection services)
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
    public static IServiceCollection AddCrdtTimestampProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICrdtTimestampProvider
    {
        services.AddSingleton<ICrdtTimestampProvider, TProvider>();
        return services;
    }
}