using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Ama.CRDT.Partitioning.Streams.UnitTests")]

namespace Ama.CRDT.Partitioning.Streams.Extensions;

using Ama.CRDT.Extensions;
using Ama.CRDT.Partitioning.Streams.Models;
using Ama.CRDT.Partitioning.Streams.Models.Serialization;
using Ama.CRDT.Partitioning.Streams.Services;
using Ama.CRDT.Partitioning.Streams.Services.Metrics;
using Ama.CRDT.Partitioning.Streams.Services.Serialization;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

/// <summary>
/// Provides extension methods for setting up CRDT stream partitioning services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class StreamPartitioningServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stream partitioning services and the custom stream provider for Ama.CRDT.
    /// </summary>
    /// <typeparam name="TProvider">The type of the stream provider to register. Must implement <see cref="IPartitionStreamProvider"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCrdtStreamPartitioning<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(this IServiceCollection services)
        where TProvider : class, IPartitionStreamProvider
    {
        // Explicitly register external stream-specific models to the polymorphic converter 
        services.AddCrdtSerializableType<BPlusTreeNode>("bplus-tree-node");
        services.AddCrdtSerializableType<BTreeHeader>("bplus-tree-header");
        services.AddCrdtSerializableType<DataStreamHeader>("data-stream-header");
        services.AddCrdtSerializableType<FreeSpaceState>("free-space-state");

        // Dynamically register our Streams-specific context to seamlessly merge 
        // with the central Ama.CRDT serialization pipeline in the DI container.
        services.AddCrdtJsonTypeInfoResolver(StreamsJsonContext.Default);

        services.TryAddSingleton<StreamsCrdtMetrics>();

        // Register core services natively to enable the AOT DI source generator,
        // and resolve them via factories on interfaces to enforce replica scope validation.
        services.AddScoped<TProvider>();
        services.AddScoped<IPartitionStreamProvider>(sp => { ValidateReplicaScope(sp, typeof(TProvider).Name); return sp.GetRequiredService<TProvider>(); });

        services.TryAddScoped<DefaultPartitionSerializationService>();
        services.TryAddScoped<IPartitionSerializationService>(sp => { ValidateReplicaScope(sp, nameof(DefaultPartitionSerializationService)); return sp.GetRequiredService<DefaultPartitionSerializationService>(); });
        
        services.TryAddScoped<StreamPartitionStorageService>();
        services.TryAddScoped<IPartitionStorageService>(sp => { ValidateReplicaScope(sp, nameof(StreamPartitionStorageService)); return sp.GetRequiredService<StreamPartitionStorageService>(); });

        return services;
    }

    private static void ValidateReplicaScope(IServiceProvider sp, string serviceName)
    {
        var replicaContext = sp.GetService<ReplicaContext>();

        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException(
                $"The service '{serviceName}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}. " +
                $"Please use {nameof(ICrdtScopeFactory)}.{nameof(ICrdtScopeFactory.CreateScope)} to create a valid CRDT scope before resolving services.");
        }
    }
}