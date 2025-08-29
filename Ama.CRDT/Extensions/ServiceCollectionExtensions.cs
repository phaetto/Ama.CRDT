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
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// builder.Services.AddCrdt();
    ///
    /// var app = builder.Build();
    ///
    /// // The recommended way to get a patcher is through the scope factory.
    /// var scopeFactory = app.Services.GetRequiredService<ICrdtScopeFactory>();
    /// using(var userScope = scopeFactory.CreateScope("user-session-abc"))
    /// {
    ///     var userPatcher = userScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
    ///     // This userPatcher is now configured for "user-session-abc"
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdt(this IServiceCollection services)
    {
        // Scoped context that holds the replica ID.
        // It's populated by CrdtScopeFactory.
        services.AddScoped<ReplicaContext>();
        
        // This factory is the entry point for creating replica-specific scopes.
        // It does not require validation itself.
        services.TryAddScoped<ICrdtScopeFactory, CrdtScopeFactory>();
        
        // Register core services with a factory that validates the scope.
        // This prevents resolving them from a non-replica scope (e.g., root container, ASP.NET request scope).
        services.TryAddScoped(CreateValidatedInstance<CrdtApplicator>);
        services.TryAddScoped<ICrdtApplicator>(sp => sp.GetRequiredService<CrdtApplicator>());

        services.TryAddScoped(CreateValidatedInstance<CrdtPatcher>);
        services.TryAddScoped<ICrdtPatcher>(sp => sp.GetRequiredService<CrdtPatcher>());
        
        services.TryAddScoped(CreateValidatedInstance<CrdtStrategyProvider>);
        services.TryAddScoped<ICrdtStrategyProvider>(sp => sp.GetRequiredService<CrdtStrategyProvider>());
        
        services.TryAddScoped(CreateValidatedInstance<ElementComparerProvider>);
        services.TryAddScoped<IElementComparerProvider>(sp => sp.GetRequiredService<ElementComparerProvider>());
        
        services.TryAddScoped(CreateValidatedInstance<CrdtMetadataManager>);
        services.TryAddScoped<ICrdtMetadataManager>(sp => sp.GetRequiredService<CrdtMetadataManager>());

        // Register the default timestamp provider with validation.
        // This can be overridden by AddCrdtTimestampProvider.
        services.TryAddScoped(CreateValidatedInstance<SequentialTimestampProvider>);
        services.TryAddScoped<ICrdtTimestampProvider>(sp => sp.GetRequiredService<SequentialTimestampProvider>());

        // Register all strategies with validation.
        services.TryAddScoped(CreateValidatedInstance<LwwStrategy>);
        services.TryAddScoped(CreateValidatedInstance<CounterStrategy>);
        services.TryAddScoped(CreateValidatedInstance<SortedSetStrategy>);
        services.TryAddScoped(CreateValidatedInstance<ArrayLcsStrategy>);
        services.TryAddScoped(CreateValidatedInstance<GCounterStrategy>);
        services.TryAddScoped(CreateValidatedInstance<BoundedCounterStrategy>);
        services.TryAddScoped(CreateValidatedInstance<MaxWinsStrategy>);
        services.TryAddScoped(CreateValidatedInstance<MinWinsStrategy>);
        services.TryAddScoped(CreateValidatedInstance<AverageRegisterStrategy>);
        services.TryAddScoped(CreateValidatedInstance<GSetStrategy>);
        services.TryAddScoped(CreateValidatedInstance<TwoPhaseSetStrategy>);
        services.TryAddScoped(CreateValidatedInstance<LwwSetStrategy>);
        services.TryAddScoped(CreateValidatedInstance<OrSetStrategy>);
        services.TryAddScoped(CreateValidatedInstance<PriorityQueueStrategy>);
        services.TryAddScoped(CreateValidatedInstance<FixedSizeArrayStrategy>);
        services.TryAddScoped(CreateValidatedInstance<LseqStrategy>);
        services.TryAddScoped(CreateValidatedInstance<VoteCounterStrategy>);
        services.TryAddScoped(CreateValidatedInstance<StateMachineStrategy>);
        services.TryAddScoped(CreateValidatedInstance<ExclusiveLockStrategy>);
        services.TryAddScoped(CreateValidatedInstance<LwwMapStrategy>);
        services.TryAddScoped(CreateValidatedInstance<OrMapStrategy>);
        
        // Register all concrete strategies as ICrdtStrategy.
        // This will resolve the concrete type, which in turn triggers our validating factory.
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
    /// builder.Services.AddCrdt();
    /// builder.Services.AddCrdtComparer<UserComparer>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtComparer<TComparer>(this IServiceCollection services)
        where TComparer : class, IElementComparer
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IElementComparer, TComparer>());
        return services;
    }
    
    /// <summary>
    /// Registers a custom timestamp provider, replacing the default <see cref="SequentialTimestampProvider"/>.
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
    /// builder.Services.AddCrdt();
    /// builder.Services.AddCrdtTimestampProvider<LogicalClockProvider>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtTimestampProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICrdtTimestampProvider
    {
        // Register the custom provider implementation with validation.
        services.AddScoped(CreateValidatedInstance<TProvider>);
        // Override the ICrdtTimestampProvider registration to point to the custom implementation.
        services.AddScoped<ICrdtTimestampProvider>(sp => sp.GetRequiredService<TProvider>());
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="ICrdtTimestamp"/> implementation for polymorphic JSON serialization.
    /// This allows the system to correctly serialize and deserialize custom timestamp types.
    /// </summary>
    /// <typeparam name="TTimestamp">The custom timestamp type to register. Must implement <see cref="ICrdtTimestamp"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="discriminator">A unique string identifier for the timestamp type, used in the JSON output.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// public readonly record struct MyCustomTimestamp(long Value) : ICrdtTimestamp
    /// {
    ///     public int CompareTo(ICrdtTimestamp other)
    ///     {
    ///         if (other is MyCustomTimestamp otherTimestamp)
    ///         {
    ///             return Value.CompareTo(otherTimestamp.Value);
    ///         }
    ///         return false; // Probably throw if you have different timestamps
    ///     }
    /// }
    /// 
    /// // In your DI setup:
    /// builder.Services.AddCrdt();
    /// builder.Services.AddCrdtTimestampType<MyCustomTimestamp>("my-custom");
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtTimestampType<TTimestamp>(this IServiceCollection services, string discriminator)
        where TTimestamp : ICrdtTimestamp
    {
        Models.Serialization.CrdtTimestampJsonConverter.Register(discriminator, typeof(TTimestamp));
        return services;
    }

    private static TImplementation CreateValidatedInstance<TImplementation>(IServiceProvider sp) where TImplementation : class
    {
        var replicaContext = sp.GetService<ReplicaContext>();

        // This check ensures that the service is only resolved within a scope created by CrdtScopeFactory,
        // which is responsible for setting the ReplicaId.
        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException(
                $"The service '{typeof(TImplementation).Name}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}. " +
                $"Please use {nameof(ICrdtScopeFactory)}.{nameof(ICrdtScopeFactory.CreateScope)} to create a valid CRDT scope before resolving services.");
        }

        return ActivatorUtilities.CreateInstance<TImplementation>(sp);
    }
}