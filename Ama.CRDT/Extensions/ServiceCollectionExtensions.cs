using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
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
    /// (e.g., LWW, Counter, ArrayLcs), the <see cref="ICrdtPatcherFactory"/> for creating replica-specific patchers,
    /// the <see cref="ICrdtApplicator"/> for applying patches, and other essential supporting services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">An action to configure the <see cref="CrdtOptions"/>. This is primarily used to set a default <c>ReplicaId</c> for services that might be resolved without a factory, although using the <see cref="ICrdtPatcherFactory"/> is the recommended approach in multi-replica environments.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="configureOptions"/> is null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// builder.Services.AddCrdt(options =>
    /// {
    ///     // This replica ID is used for the default singleton ICrdtPatcher.
    ///     options.ReplicaId = "server-node-1";
    /// });
    ///
    /// var app = builder.Build();
    ///
    /// // The recommended way to get a patcher is through the factory,
    /// // especially in a multi-user or multi-node environment.
    /// var patcherFactory = app.Services.GetRequiredService<ICrdtPatcherFactory>();
    /// var userPatcher = patcherFactory.Create("user-session-abc");
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

        services.TryAddSingleton<ICrdtApplicator, CrdtApplicator>();
        services.TryAddSingleton<ICrdtPatcher, CrdtPatcher>();

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
        services.TryAddSingleton<GSetStrategy>();
        services.TryAddSingleton<TwoPhaseSetStrategy>();
        services.TryAddSingleton<LwwSetStrategy>();
        services.TryAddSingleton<OrSetStrategy>();
        services.TryAddSingleton<PriorityQueueStrategy>();
        services.TryAddSingleton<FixedSizeArrayStrategy>();
        services.TryAddSingleton<LseqStrategy>();
        services.TryAddSingleton<VoteCounterStrategy>();
        services.TryAddSingleton<StateMachineStrategy>();
        services.TryAddSingleton<ExclusiveLockStrategy>();

        services.AddSingleton<ICrdtStrategy, LwwStrategy>(sp => sp.GetRequiredService<LwwStrategy>());
        services.AddSingleton<ICrdtStrategy, CounterStrategy>(sp => sp.GetRequiredService<CounterStrategy>());
        services.AddSingleton<ICrdtStrategy, SortedSetStrategy>(sp => sp.GetRequiredService<SortedSetStrategy>());
        services.AddSingleton<ICrdtStrategy, ArrayLcsStrategy>(sp => sp.GetRequiredService<ArrayLcsStrategy>());
        services.AddSingleton<ICrdtStrategy, GCounterStrategy>(sp => sp.GetRequiredService<GCounterStrategy>());
        services.AddSingleton<ICrdtStrategy, BoundedCounterStrategy>(sp => sp.GetRequiredService<BoundedCounterStrategy>());
        services.AddSingleton<ICrdtStrategy, MaxWinsStrategy>(sp => sp.GetRequiredService<MaxWinsStrategy>());
        services.AddSingleton<ICrdtStrategy, MinWinsStrategy>(sp => sp.GetRequiredService<MinWinsStrategy>());
        services.AddSingleton<ICrdtStrategy, AverageRegisterStrategy>(sp => sp.GetRequiredService<AverageRegisterStrategy>());
        services.AddSingleton<ICrdtStrategy, GSetStrategy>(sp => sp.GetRequiredService<GSetStrategy>());
        services.AddSingleton<ICrdtStrategy, TwoPhaseSetStrategy>(sp => sp.GetRequiredService<TwoPhaseSetStrategy>());
        services.AddSingleton<ICrdtStrategy, LwwSetStrategy>(sp => sp.GetRequiredService<LwwSetStrategy>());
        services.AddSingleton<ICrdtStrategy, OrSetStrategy>(sp => sp.GetRequiredService<OrSetStrategy>());
        services.AddSingleton<ICrdtStrategy, PriorityQueueStrategy>(sp => sp.GetRequiredService<PriorityQueueStrategy>());
        services.AddSingleton<ICrdtStrategy, FixedSizeArrayStrategy>(sp => sp.GetRequiredService<FixedSizeArrayStrategy>());
        services.AddSingleton<ICrdtStrategy, LseqStrategy>(sp => sp.GetRequiredService<LseqStrategy>());
        services.AddSingleton<ICrdtStrategy, VoteCounterStrategy>(sp => sp.GetRequiredService<VoteCounterStrategy>());
        services.AddSingleton<ICrdtStrategy, StateMachineStrategy>(sp => sp.GetRequiredService<StateMachineStrategy>());
        services.AddSingleton<ICrdtStrategy, ExclusiveLockStrategy>(sp => sp.GetRequiredService<ExclusiveLockStrategy>());

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