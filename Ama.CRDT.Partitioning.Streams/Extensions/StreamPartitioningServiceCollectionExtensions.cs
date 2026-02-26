namespace Ama.CRDT.Extensions;

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
    public static IServiceCollection AddCrdtStreamPartitioning<TProvider>(this IServiceCollection services)
        where TProvider : class, IPartitionStreamProvider
    {
        services.TryAddSingleton<StreamsCrdtMetrics>();

        services.AddScoped(CreateValidatedInstance<TProvider>);
        services.AddScoped<IPartitionStreamProvider>(sp => sp.GetRequiredService<TProvider>());

        services.TryAddScoped(CreateValidatedInstance<DefaultPartitionSerializationService>);
        services.TryAddScoped<IPartitionSerializationService>(sp => sp.GetRequiredService<DefaultPartitionSerializationService>());
        
        services.TryAddScoped(CreateValidatedInstance<StreamPartitionStorageService>);
        services.TryAddScoped<IPartitionStorageService>(sp => sp.GetRequiredService<StreamPartitionStorageService>());

        return services;
    }

    private static TImplementation CreateValidatedInstance<TImplementation>(IServiceProvider sp) where TImplementation : class
    {
        var replicaContext = sp.GetService<ReplicaContext>();

        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException(
                $"The service '{typeof(TImplementation).Name}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}. " +
                $"Please use {nameof(ICrdtScopeFactory)}.{nameof(ICrdtScopeFactory.CreateScope)} to create a valid CRDT scope before resolving services.");
        }

        return ActivatorUtilities.CreateInstance<TImplementation>(sp);
    }
}