using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: InternalsVisibleTo("Ama.CRDT.UnitTests")]
[assembly: InternalsVisibleTo("Ama.CRDT.Benchmarks")]

namespace Ama.CRDT.Extensions;

/// <summary>
/// Provides extension methods for setting up CRDT services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core CRDT services to the specified <see cref="IServiceCollection"/>. This includes all default CRDT strategies
    /// (e.g., LWW, Counter, ArrayLcs), the <see cref="ICrdtScopeFactory"/> for creating replica-specific service scopes,
    /// the <see cref="ICrdtApplicator"/> for applying patches, and other essential supporting services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">An action to configure global <see cref="CrdtOptions"/>. Note that the <c>ReplicaId</c> is managed per-scope and should not be set here.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="configureOptions"/> is null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// builder.Services.AddCrdt(options =>
    /// {
    ///     // Configure global options here if needed in the future.
    /// });
    ///
    /// var app = builder.Build();
    ///
    /// // The recommended way to get a patcher is through the scope factory.
    /// var scopeFactory = app.Services.GetRequiredService<ICrdtScopeFactory>();
    /// using(var userScope = scopeFactory.CreateScope("user-session-abc"))
    /// {
    ///     var userPatcher = userScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdt(this IServiceCollection services, Action<CrdtOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        // Singleton services that are stateless and shared across all replicas
        services.TryAddSingleton<ICrdtScopeFactory, CrdtScopeFactory>();
        services.TryAddSingleton<IElementComparerProvider, ElementComparerProvider>();
        services.TryAddSingleton<ICrdtTimestampProvider, EpochTimestampProvider>();
        services.TryAddSingleton<ICrdtMetadataManager, CrdtMetadataManager>();

        // Scoped services that hold state or depend on the replicaId
        services.AddScoped<ReplicaContext>();
        services.TryAddScoped<ICrdtApplicator, CrdtApplicator>();
        services.TryAddScoped<ICrdtPatcher, CrdtPatcher>();
        services.TryAddScoped<ICrdtStrategyProvider, CrdtStrategyProvider>();

        // Register all strategies with a scoped lifetime
        services.TryAddScoped<LwwStrategy>();
        services.TryAddScoped<CounterStrategy>();
        services.TryAddScoped<SortedSetStrategy>();
        services.TryAddScoped<ArrayLcsStrategy>();
        services.TryAddScoped<GCounterStrategy>();
        services.TryAddScoped<BoundedCounterStrategy>();
        services.TryAddScoped<MaxWinsStrategy>();
        services.TryAddScoped<MinWinsStrategy>();
        services.TryAddScoped<AverageRegisterStrategy>();
        services.TryAddScoped<GSetStrategy>();
        services.TryAddScoped<TwoPhaseSetStrategy>();
        services.TryAddScoped<LwwSetStrategy>();
        services.TryAddScoped<OrSetStrategy>();
        services.TryAddScoped<PriorityQueueStrategy>();
        services.TryAddScoped<FixedSizeArrayStrategy>();
        services.TryAddScoped<LseqStrategy>();
        services.TryAddScoped<VoteCounterStrategy>();
        services.TryAddScoped<StateMachineStrategy>();
        services.TryAddScoped<ExclusiveLockStrategy>();
        services.TryAddScoped<LwwMapStrategy>();
        services.TryAddScoped<OrMapStrategy>();

        services.AddScoped<ICrdtStrategy, LwwStrategy>(sp => sp.GetRequiredService<LwwStrategy>());
        services.AddScoped<ICrdtStrategy, CounterStrategy>(sp => sp.GetRequiredService<CounterStrategy>());
        services.AddScoped<ICrdtStrategy, SortedSetStrategy>(sp => sp.GetRequiredService<SortedSetStrategy>());
        services.AddScoped<ICrdtStrategy, ArrayLcsStrategy>(sp => sp.GetRequiredService<ArrayLcsStrategy>());
        services.AddScoped<ICrdtStrategy, GCounterStrategy>(sp => sp.GetRequiredService<GCounterStrategy>());
        services.AddScoped<ICrdtStrategy, BoundedCounterStrategy>(sp => sp.GetRequiredService<BoundedCounterStrategy>());
        services.AddScoped<ICrdtStrategy, MaxWinsStrategy>(sp => sp.GetRequiredService<MaxWinsStrategy>());
        services.AddScoped<ICrdtStrategy, MinWinsStrategy>(sp => sp.GetRequiredService<MinWinsStrategy>());
        services.AddScoped<ICrdtStrategy, AverageRegisterStrategy>(sp => sp.GetRequiredService<AverageRegisterStrategy>());
        services.AddScoped<ICrdtStrategy, GSetStrategy>(sp => sp.GetRequiredService<GSetStrategy>());
        services.AddScoped<ICrdtStrategy, TwoPhaseSetStrategy>(sp => sp.GetRequiredService<TwoPhaseSetStrategy>());
        services.AddScoped<ICrdtStrategy, LwwSetStrategy>(sp => sp.GetRequiredService<LwwSetStrategy>());
        services.AddScoped<ICrdtStrategy, OrSetStrategy>(sp => sp.GetRequiredService<OrSetStrategy>());
        services.AddScoped<ICrdtStrategy, PriorityQueueStrategy>(sp => sp.GetRequiredService<PriorityQueueStrategy>());
        services.AddScoped<ICrdtStrategy, FixedSizeArrayStrategy>(sp => sp.GetRequiredService<FixedSizeArrayStrategy>());
        services.AddScoped<ICrdtStrategy, LseqStrategy>(sp => sp.GetRequiredService<LseqStrategy>());
        services.AddScoped<ICrdtStrategy, VoteCounterStrategy>(sp => sp.GetRequiredService<VoteCounterStrategy>());
        services.AddScoped<ICrdtStrategy, StateMachineStrategy>(sp => sp.GetRequiredService<StateMachineStrategy>());
        services.AddScoped<ICrdtStrategy, ExclusiveLockStrategy>(sp => sp.GetRequiredService<ExclusiveLockStrategy>());
        services.AddScoped<ICrdtStrategy, LwwMapStrategy>(sp => sp.GetRequiredService<LwwMapStrategy>());
        services.AddScoped<ICrdtStrategy, OrMapStrategy>(sp => sp.GetRequiredService<OrMapStrategy>());

        return services;
    }

    /// <summary>
    /// Registers a custom comparer for identifying elements within collections managed by strategies like <see cref="ArrayLcsStrategy"/>.
    /// This is necessary when reference or default equality is insufficient for determining if two objects represent the same entity (e.g., comparing by a unique ID property).
    /// </summary>
    /// <typeparam name="TComparer">The type of the comparer to register. Must implement <see cref="IElementComparer"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Define a custom comparer for a User object that compares by ID.
    /// public class UserComparer : IElementComparer
    /// {
    ///     public bool CanCompare(Type type) => type == typeof(User);
    /// 
    ///     public new bool Equals(object x, object y)
    ///     {
    ///         if (x is User userX && y is User userY)
    ///         {
    ///             return userX.Id == userY.Id;
    ///         }
    ///         return object.Equals(x, y);
    ///     }
    /// 
    ///     public int GetHashCode(object obj)
    ///     {
    ///         return (obj is User user) ? user.Id.GetHashCode() : obj.GetHashCode();
    ///     }
    /// }
    /// 
    /// // In your DI setup:
    /// builder.Services.AddCrdt(options => { ... });
    /// builder.Services.AddCrdtComparer<UserComparer>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtComparer<TComparer>(this IServiceCollection services)
        where TComparer : class, IElementComparer
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IElementComparer, TComparer>());
        return services;
    }
    
    /// <summary>
    /// Registers a custom timestamp provider, replacing the default <see cref="EpochTimestampProvider"/>.
    /// This allows for the integration of different clock types, such as logical or hybrid clocks, which can provide stronger causality guarantees in distributed systems than wall-clock time.
    /// </summary>
    /// <typeparam name="TProvider">The type of the timestamp provider to register. Must implement <see cref="ICrdtTimestampProvider"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // A simple logical clock timestamp provider.
    /// public sealed class LogicalClockProvider : ICrdtTimestampProvider
    /// {
    ///     private long counter = 0;
    /// 
    ///     public ICrdtTimestamp Now()
    ///     {
    ///         long timestamp = Interlocked.Increment(ref counter);
    ///         return new EpochTimestamp(timestamp); // Using EpochTimestamp to wrap the logical value.
    ///     }
    /// }
    /// 
    /// // In your DI setup:
    /// builder.Services.AddCrdt(options => { ... });
    /// builder.Services.AddCrdtTimestampProvider<LogicalClockProvider>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtTimestampProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICrdtTimestampProvider
    {
        services.AddSingleton<ICrdtTimestampProvider, TProvider>();
        return services;
    }
}