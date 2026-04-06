using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Models.Serialization.Converters;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Adapters;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Serialization;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Services.Strategies.Decorators;
using Ama.CRDT.Services.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: InternalsVisibleTo("Ama.CRDT.UnitTests")]
[assembly: InternalsVisibleTo("Ama.CRDT.PropertyTests")]
[assembly: InternalsVisibleTo("Ama.CRDT.Benchmarks")]
[assembly: InternalsVisibleTo("Ama.CRDT.Partitioning.Streams.UnitTests")]

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
    /// <param name="configure">An optional action to configure CRDT strategies and decorators via the Fluent API without using attributes.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// builder.Services.AddCrdt(options => 
    /// {
    ///     options.Entity<MyDocument>()
    ///         .Property(x => x.Metrics).HasStrategy<MinWinsMapStrategy>();
    /// });
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
    public static IServiceCollection AddCrdt(this IServiceCollection services, Action<CrdtModelBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Setup the default System.Text.Json serialization integration.
        // This is extracted into its own method to allow for future binary serialization abstraction.
        services.AddCrdtSystemTextJson();

        // Build the configuration registry
        var builder = new CrdtModelBuilder();
        configure?.Invoke(builder);
        services.TryAddSingleton(builder.Build());

        // Register the internal CRDT AOT Context for reflection-free access to library models
        services.AddCrdtAotContext<InternalCrdtAotContext>();

        // Add metrics
        services.TryAddSingleton<PartitionManagerCrdtMetrics>();

        // Pure utility services that don't depend on replica scope
        services.TryAddSingleton<IVersionVectorSyncService, VersionVectorSyncService>();

        // Scoped context that holds the replica ID.
        // It's populated by CrdtScopeFactory.
        services.AddScoped<ReplicaContext>();
        
        // This factory is the entry point for creating replica-specific scopes.
        // It does not require validation itself.
        services.TryAddSingleton<ICrdtScopeFactory, CrdtScopeFactory>();
        
        // Register core services natively to enable the AOT DI source generator,
        // and resolve them via factories on interfaces to enforce replica scope validation.
        // This prevents resolving them from a non-replica scope (e.g., root container, ASP.NET request scope).
        services.TryAddScoped<CrdtApplicator>();
        services.TryAddScoped<ICrdtApplicator>(sp => { ValidateReplicaScope(sp, nameof(CrdtApplicator)); return sp.GetRequiredService<CrdtApplicator>(); });
        
        // Base asynchronous pipeline executor. Translates IAsyncCrdtApplicator to ICrdtApplicator.
        services.TryAddScoped<IAsyncCrdtApplicator>(sp => new AsyncCrdtApplicatorAdapter(sp.GetRequiredService<ICrdtApplicator>()));

        services.TryAddScoped<CrdtPatcher>();
        services.TryAddScoped<ICrdtPatcher>(sp => { ValidateReplicaScope(sp, nameof(CrdtPatcher)); return sp.GetRequiredService<CrdtPatcher>(); });

        // Base asynchronous pipeline executor. Translates IAsyncCrdtPatcher to ICrdtPatcher.
        services.TryAddScoped<IAsyncCrdtPatcher>(sp => new AsyncCrdtPatcherAdapter(sp.GetRequiredService<ICrdtPatcher>()));
        
        services.TryAddScoped<CrdtStrategyProvider>();
        services.TryAddScoped<ICrdtStrategyProvider>(sp => { ValidateReplicaScope(sp, nameof(CrdtStrategyProvider)); return sp.GetRequiredService<CrdtStrategyProvider>(); });
        
        services.TryAddScoped<ElementComparerProvider>();
        services.TryAddScoped<IElementComparerProvider>(sp => { ValidateReplicaScope(sp, nameof(ElementComparerProvider)); return sp.GetRequiredService<ElementComparerProvider>(); });
        
        services.TryAddScoped<CrdtMetadataManager>();
        services.TryAddScoped<ICrdtMetadataManager>(sp => { ValidateReplicaScope(sp, nameof(CrdtMetadataManager)); return sp.GetRequiredService<CrdtMetadataManager>(); });

        // Register Partitioning services
        services.TryAddScoped(typeof(IPartitionManager<>), typeof(PartitionManager<>));

        // Register the default timestamp provider with validation.
        // This can be overridden by AddCrdtTimestampProvider.
        services.TryAddScoped<EpochTimestampProvider>();
        services.TryAddScoped<ICrdtTimestampProvider>(sp => { ValidateReplicaScope(sp, nameof(EpochTimestampProvider)); return sp.GetRequiredService<EpochTimestampProvider>(); });

        // Register all strategies to leverage the AOT DI source generator.
        services.TryAddScoped<LwwStrategy>();
        services.TryAddScoped<FwwStrategy>();
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
        services.TryAddScoped<FwwSetStrategy>();
        services.TryAddScoped<OrSetStrategy>();
        services.TryAddScoped<PriorityQueueStrategy>();
        services.TryAddScoped<FixedSizeArrayStrategy>();
        services.TryAddScoped<LseqStrategy>();
        services.TryAddScoped<VoteCounterStrategy>();
        services.TryAddScoped<StateMachineStrategy>();
        services.TryAddScoped<LwwMapStrategy>();
        services.TryAddScoped<FwwMapStrategy>();
        services.TryAddScoped<OrMapStrategy>();
        services.TryAddScoped<CounterMapStrategy>();
        services.TryAddScoped<MaxWinsMapStrategy>();
        services.TryAddScoped<MinWinsMapStrategy>();
        services.TryAddScoped<GraphStrategy>();
        services.TryAddScoped<TwoPhaseGraphStrategy>();
        services.TryAddScoped<ReplicatedTreeStrategy>();
        services.TryAddScoped<RgaStrategy>();
        services.TryAddScoped<EpochBoundStrategy>();
        services.TryAddScoped<ApprovalQuorumStrategy>();

        // Register all concrete strategies as ICrdtStrategy.
        // This will resolve the concrete type, which in turn triggers our validating factory.
        services.AddScoped<ICrdtStrategy, LwwStrategy>(sp => { ValidateReplicaScope(sp, nameof(LwwStrategy)); return sp.GetRequiredService<LwwStrategy>(); });
        services.AddScoped<ICrdtStrategy, FwwStrategy>(sp => { ValidateReplicaScope(sp, nameof(FwwStrategy)); return sp.GetRequiredService<FwwStrategy>(); });
        services.AddScoped<ICrdtStrategy, CounterStrategy>(sp => { ValidateReplicaScope(sp, nameof(CounterStrategy)); return sp.GetRequiredService<CounterStrategy>(); });
        services.AddScoped<ICrdtStrategy, SortedSetStrategy>(sp => { ValidateReplicaScope(sp, nameof(SortedSetStrategy)); return sp.GetRequiredService<SortedSetStrategy>(); });
        services.AddScoped<ICrdtStrategy, ArrayLcsStrategy>(sp => { ValidateReplicaScope(sp, nameof(ArrayLcsStrategy)); return sp.GetRequiredService<ArrayLcsStrategy>(); });
        services.AddScoped<ICrdtStrategy, GCounterStrategy>(sp => { ValidateReplicaScope(sp, nameof(GCounterStrategy)); return sp.GetRequiredService<GCounterStrategy>(); });
        services.AddScoped<ICrdtStrategy, BoundedCounterStrategy>(sp => { ValidateReplicaScope(sp, nameof(BoundedCounterStrategy)); return sp.GetRequiredService<BoundedCounterStrategy>(); });
        services.AddScoped<ICrdtStrategy, MaxWinsStrategy>(sp => { ValidateReplicaScope(sp, nameof(MaxWinsStrategy)); return sp.GetRequiredService<MaxWinsStrategy>(); });
        services.AddScoped<ICrdtStrategy, MinWinsStrategy>(sp => { ValidateReplicaScope(sp, nameof(MinWinsStrategy)); return sp.GetRequiredService<MinWinsStrategy>(); });
        services.AddScoped<ICrdtStrategy, AverageRegisterStrategy>(sp => { ValidateReplicaScope(sp, nameof(AverageRegisterStrategy)); return sp.GetRequiredService<AverageRegisterStrategy>(); });
        services.AddScoped<ICrdtStrategy, GSetStrategy>(sp => { ValidateReplicaScope(sp, nameof(GSetStrategy)); return sp.GetRequiredService<GSetStrategy>(); });
        services.AddScoped<ICrdtStrategy, TwoPhaseSetStrategy>(sp => { ValidateReplicaScope(sp, nameof(TwoPhaseSetStrategy)); return sp.GetRequiredService<TwoPhaseSetStrategy>(); });
        services.AddScoped<ICrdtStrategy, LwwSetStrategy>(sp => { ValidateReplicaScope(sp, nameof(LwwSetStrategy)); return sp.GetRequiredService<LwwSetStrategy>(); });
        services.AddScoped<ICrdtStrategy, FwwSetStrategy>(sp => { ValidateReplicaScope(sp, nameof(FwwSetStrategy)); return sp.GetRequiredService<FwwSetStrategy>(); });
        services.AddScoped<ICrdtStrategy, OrSetStrategy>(sp => { ValidateReplicaScope(sp, nameof(OrSetStrategy)); return sp.GetRequiredService<OrSetStrategy>(); });
        services.AddScoped<ICrdtStrategy, PriorityQueueStrategy>(sp => { ValidateReplicaScope(sp, nameof(PriorityQueueStrategy)); return sp.GetRequiredService<PriorityQueueStrategy>(); });
        services.AddScoped<ICrdtStrategy, FixedSizeArrayStrategy>(sp => { ValidateReplicaScope(sp, nameof(FixedSizeArrayStrategy)); return sp.GetRequiredService<FixedSizeArrayStrategy>(); });
        services.AddScoped<ICrdtStrategy, LseqStrategy>(sp => { ValidateReplicaScope(sp, nameof(LseqStrategy)); return sp.GetRequiredService<LseqStrategy>(); });
        services.AddScoped<ICrdtStrategy, VoteCounterStrategy>(sp => { ValidateReplicaScope(sp, nameof(VoteCounterStrategy)); return sp.GetRequiredService<VoteCounterStrategy>(); });
        services.AddScoped<ICrdtStrategy, StateMachineStrategy>(sp => { ValidateReplicaScope(sp, nameof(StateMachineStrategy)); return sp.GetRequiredService<StateMachineStrategy>(); });
        services.AddScoped<ICrdtStrategy, LwwMapStrategy>(sp => { ValidateReplicaScope(sp, nameof(LwwMapStrategy)); return sp.GetRequiredService<LwwMapStrategy>(); });
        services.AddScoped<ICrdtStrategy, FwwMapStrategy>(sp => { ValidateReplicaScope(sp, nameof(FwwMapStrategy)); return sp.GetRequiredService<FwwMapStrategy>(); });
        services.AddScoped<ICrdtStrategy, OrMapStrategy>(sp => { ValidateReplicaScope(sp, nameof(OrMapStrategy)); return sp.GetRequiredService<OrMapStrategy>(); });
        services.AddScoped<ICrdtStrategy, CounterMapStrategy>(sp => { ValidateReplicaScope(sp, nameof(CounterMapStrategy)); return sp.GetRequiredService<CounterMapStrategy>(); });
        services.AddScoped<ICrdtStrategy, MaxWinsMapStrategy>(sp => { ValidateReplicaScope(sp, nameof(MaxWinsMapStrategy)); return sp.GetRequiredService<MaxWinsMapStrategy>(); });
        services.AddScoped<ICrdtStrategy, MinWinsMapStrategy>(sp => { ValidateReplicaScope(sp, nameof(MinWinsMapStrategy)); return sp.GetRequiredService<MinWinsMapStrategy>(); });
        services.AddScoped<ICrdtStrategy, GraphStrategy>(sp => { ValidateReplicaScope(sp, nameof(GraphStrategy)); return sp.GetRequiredService<GraphStrategy>(); });
        services.AddScoped<ICrdtStrategy, TwoPhaseGraphStrategy>(sp => { ValidateReplicaScope(sp, nameof(TwoPhaseGraphStrategy)); return sp.GetRequiredService<TwoPhaseGraphStrategy>(); });
        services.AddScoped<ICrdtStrategy, ReplicatedTreeStrategy>(sp => { ValidateReplicaScope(sp, nameof(ReplicatedTreeStrategy)); return sp.GetRequiredService<ReplicatedTreeStrategy>(); });
        services.AddScoped<ICrdtStrategy, RgaStrategy>(sp => { ValidateReplicaScope(sp, nameof(RgaStrategy)); return sp.GetRequiredService<RgaStrategy>(); });
        services.AddScoped<ICrdtStrategy, EpochBoundStrategy>(sp => { ValidateReplicaScope(sp, nameof(EpochBoundStrategy)); return sp.GetRequiredService<EpochBoundStrategy>(); });
        services.AddScoped<ICrdtStrategy, ApprovalQuorumStrategy>(sp => { ValidateReplicaScope(sp, nameof(ApprovalQuorumStrategy)); return sp.GetRequiredService<ApprovalQuorumStrategy>(); });

        return services;
    }

    /// <summary>
    /// Binds the <see cref="System.Text.Json"/> format as the core <see cref="ICrdtSerializer"/> mechanism for the CRDT library.
    /// Called by default by <see cref="AddCrdt"/>, but extracted to allow swapping to binary protocols.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCrdtSystemTextJson(this IServiceCollection services)
    {
        // Setup central JSON options for CRDT natively in the DI container.
        services.TryAddKeyedSingleton("Ama.CRDT", (sp, key) =>
        {
            var customResolvers = sp.GetKeyedServices<IJsonTypeInfoResolver>("Ama.CRDT");
            var resolvers = new List<IJsonTypeInfoResolver> { CrdtJsonContext.Default };
            
            if (customResolvers != null)
            {
                resolvers.AddRange(customResolvers);
            }

            var combinedResolver = JsonTypeInfoResolver.Combine([.. resolvers])
                .WithAddedModifier(CrdtJsonTypeInfoResolver.ApplyCrdtModifiers)
                .WithAddedModifier(CrdtMetadataJsonResolver.ApplyMetadataModifiers);

            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = combinedResolver
            };

            var crdtContexts = sp.GetServices<CrdtAotContext>();

            options.Converters.Add(CrdtPayloadJsonConverterFactory.Instance);
            options.Converters.Add(new ObjectKeyDictionaryJsonConverter(crdtContexts));

            return options;
        });

        // Register the concrete STJ implementation of the ICrdtSerializer abstraction
        services.TryAddSingleton<ICrdtSerializer, JsonCrdtSerializer>();

        return services;
    }

    /// <summary>
    /// Decorates the previously registered <see cref="IAsyncCrdtApplicator"/> with the specified decorator type.
    /// This allows building a pipeline of applicators (e.g., adding journaling or partitioning natively into the DI container).
    /// </summary>
    /// <typeparam name="TDecorator">The decorator implementation of <see cref="IAsyncCrdtApplicator"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="behavior">The explicitly required behavior for this decorator, if it needs to be overridden or directly instantiated by DI.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// builder.Services.AddCrdt()
    ///                 .AddCrdtApplicatorDecorator<PartitioningApplicatorDecorator>(DecoratorBehavior.Complex);
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtApplicatorDecorator<TDecorator>(this IServiceCollection services, DecoratorBehavior behavior)
        where TDecorator : class, IAsyncCrdtApplicator
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.DecorateService<IAsyncCrdtApplicator, TDecorator>(behavior);
    }

    /// <summary>
    /// Decorates the previously registered <see cref="IAsyncCrdtPatcher"/> with the specified decorator type.
    /// This allows building a pipeline of patchers (e.g., adding journaling natively into the DI container).
    /// </summary>
    /// <typeparam name="TDecorator">The decorator implementation of <see cref="IAsyncCrdtPatcher"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="behavior">The explicitly required behavior for this decorator, if it needs to be overridden or directly instantiated by DI.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// builder.Services.AddCrdt()
    ///                 .AddCrdtPatcherDecorator<JournalingPatcherDecorator>(DecoratorBehavior.After);
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtPatcherDecorator<TDecorator>(this IServiceCollection services, DecoratorBehavior behavior)
        where TDecorator : class, IAsyncCrdtPatcher
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.DecorateService<IAsyncCrdtPatcher, TDecorator>(behavior);
    }

    /// <summary>
    /// Registers a custom journaling service.
    /// To enable automatic journaling of generated and applied operations, you must also decorate the applicator and patcher 
    /// using <see cref="AddCrdtApplicatorDecorator{TDecorator}"/> and <see cref="AddCrdtPatcherDecorator{TDecorator}"/>.
    /// Ensure <c>AddCrdt()</c> is called prior to invoking this method.
    /// </summary>
    /// <typeparam name="TJournal">The custom implementation of <see cref="ICrdtOperationJournal"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// builder.Services.AddCrdt()
    ///                 .AddCrdtJournaling<MyDatabaseJournal>()
    ///                 .AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>(DecoratorBehavior.After)
    ///                 .AddCrdtPatcherDecorator<JournalingPatcherDecorator>(DecoratorBehavior.After);
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtJournaling<TJournal>(this IServiceCollection services)
        where TJournal : class, ICrdtOperationJournal
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the user-provided journal implementation
        services.TryAddScoped<ICrdtOperationJournal, TJournal>();

        // Register the journal manager to be used for finding missing operations
        services.TryAddScoped<IJournalManager, JournalManager>();

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
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IElementComparer, TComparer>());
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
    /// builder.Services.AddCrdtTimestampProvider<LogicalClockProvider>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtTimestampProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICrdtTimestampProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the custom provider implementation natively
        services.TryAddScoped<TProvider>();
        
        // Override the ICrdtTimestampProvider registration to point to the custom implementation.
        services.AddScoped<ICrdtTimestampProvider>(sp => {
            ValidateReplicaScope(sp, typeof(TProvider).Name);
            return sp.GetRequiredService<TProvider>();
        });
        
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
    /// builder.Services.AddCrdtTimestampType<MyCustomTimestamp>("my-custom");
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtTimestampType<TTimestamp>(this IServiceCollection services, string discriminator)
        where TTimestamp : ICrdtTimestamp
    {
        ArgumentNullException.ThrowIfNull(services);
        CrdtTypeRegistry.Register(discriminator, typeof(TTimestamp));
        return services;
    }

    /// <summary>
    /// Registers a custom type for polymorphic JSON serialization, which is necessary for types that may appear
    /// in an <c>object</c> property, such as a <see cref="CrdtOperation.Value"/> payload.
    /// </summary>
    /// <typeparam name="T">The custom type to register.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="discriminator">A unique string identifier for the type, used in the JSON output.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// public sealed record MyCustomPayload(Guid Id, string Data);
    /// 
    /// // In your DI setup:
    /// builder.Services.AddCrdtSerializableType<MyCustomPayload>("my-payload");
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtSerializableType<T>(this IServiceCollection services, string discriminator)
    {
        ArgumentNullException.ThrowIfNull(services);
        CrdtTypeRegistry.Register(discriminator, typeof(T));
        return services;
    }

    /// <summary>
    /// Registers an AOT-generated <see cref="CrdtAotContext"/> into the DI container as a Singleton.
    /// This enables reflection-free property access and instantiation for CRDT operations, making the library Native AOT compatible.
    /// Because the type metadata does not hold replica state, it's safe and efficient to register it as a singleton.
    /// </summary>
    /// <typeparam name="TContext">The generated AOT context type.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // In your DI setup:
    /// builder.Services.AddCrdtAotContext<MyAotContext>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtAotContext<TContext>(this IServiceCollection services)
        where TContext : CrdtAotContext, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<CrdtAotContext, TContext>());
        return services;
    }

    /// <summary>
    /// Registers an explicitly instantiated AOT-generated <see cref="CrdtAotContext"/> into the DI container as a Singleton.
    /// This enables reflection-free property access and instantiation for CRDT operations, making the library Native AOT compatible.
    /// Because the type metadata does not hold replica state, it's safe and efficient to register it as a singleton.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="context">The pre-instantiated context to register.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // In your DI setup:
    /// builder.Services.AddCrdtAotContext(new MyAotContext());
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtAotContext(this IServiceCollection services, CrdtAotContext context)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);
        services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(CrdtAotContext), context));
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IJsonTypeInfoResolver"/> (such as a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>)
    /// to be included in the internal CRDT serialization pipelines as a Keyed Service. 
    /// This is required for Native AOT environments when using custom payloads or custom timestamps.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="resolver">The custom type info resolver to register.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // In your DI setup:
    /// builder.Services.AddCrdtJsonTypeInfoResolver(MyCustomJsonContext.Default);
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtJsonTypeInfoResolver(this IServiceCollection services, IJsonTypeInfoResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        // Registering as a Keyed Service ensures we don't pollute the host application's default JSON resolvers 
        // while allowing our internal serialization pipeline to uniquely retrieve it.
        services.AddKeyedSingleton("Ama.CRDT", resolver);
        return services;
    }

    /// <summary>
    /// Registers a compaction policy factory for Garbage Collection and metadata pruning. 
    /// If multiple factories are registered, they are all applied sequentially.
    /// </summary>
    /// <typeparam name="TFactory">The type of the compaction policy factory to register. Must implement <see cref="ICompactionPolicyFactory"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// builder.Services.AddCrdtCompactionPolicyFactory<MyCompactionPolicyFactory>();
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtCompactionPolicyFactory<TFactory>(this IServiceCollection services)
        where TFactory : class, ICompactionPolicyFactory
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICompactionPolicyFactory, TFactory>());
        return services;
    }

    /// <summary>
    /// Registers a compaction policy factory for Garbage Collection and metadata pruning using a specific implementation factory.
    /// </summary>
    /// <typeparam name="TFactory">The type of the compaction policy factory to register. Must implement <see cref="ICompactionPolicyFactory"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="implementationFactory">The factory that creates the policy factory.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// builder.Services.AddCrdtCompactionPolicyFactory(sp => new ThresholdCompactionPolicyFactory(TimeSpan.FromDays(7)));
    /// ]]>
    /// </code>
    /// </example>
    public static IServiceCollection AddCrdtCompactionPolicyFactory<TFactory>(this IServiceCollection services, Func<IServiceProvider, TFactory> implementationFactory)
        where TFactory : class, ICompactionPolicyFactory
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationFactory);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICompactionPolicyFactory, TFactory>(implementationFactory));
        return services;
    }

    private static IServiceCollection DecorateService<TInterface, TDecorator>(this IServiceCollection services, DecoratorBehavior behavior)
        where TInterface : class
        where TDecorator : class, TInterface
    {
        var existingDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(TInterface));
        if (existingDescriptor == null)
        {
            throw new InvalidOperationException($"Cannot decorate {typeof(TInterface).Name} because it has not been registered. Ensure AddCrdt() is called first.");
        }

        services.Remove(existingDescriptor);

        services.Add(new ServiceDescriptor(typeof(TInterface), sp =>
        {
            // Resolve the inner instance correctly handling factories or instance types
            TInterface inner;
            if (existingDescriptor.ImplementationInstance != null)
            {
                inner = (TInterface)existingDescriptor.ImplementationInstance;
            }
            else if (existingDescriptor.ImplementationFactory != null)
            {
                inner = (TInterface)existingDescriptor.ImplementationFactory(sp);
            }
            else
            {
                inner = (TInterface)ActivatorUtilities.GetServiceOrCreateInstance(sp, existingDescriptor.ImplementationType!);
            }

            // Create the decorator, injecting the inner service
            return ActivatorUtilities.CreateInstance<TDecorator>(sp, inner, behavior);
        }, existingDescriptor.Lifetime));

        return services;
    }

    private static void ValidateReplicaScope(IServiceProvider sp, string serviceName)
    {
        var replicaContext = sp.GetService<ReplicaContext>();
        
        // This check ensures that the service is only resolved within a scope created by CrdtScopeFactory,
        // which is responsible for setting the ReplicaId.
        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException(
                $"The service '{serviceName}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}. " +
                $"Please use {nameof(ICrdtScopeFactory)}.{nameof(ICrdtScopeFactory.CreateScope)} to create a valid CRDT scope before resolving services.");
        }
    }
}