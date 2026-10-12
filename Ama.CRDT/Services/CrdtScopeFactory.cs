namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

/// <inheritdoc/>
internal sealed class CrdtScopeFactory : ICrdtScopeFactory
{
    private readonly IServiceProvider serviceProvider;

    public CrdtScopeFactory(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        this.serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public IServiceScope CreateScope([DisallowNull] string replicaId)
    {
        return CreateScope(replicaId, new ShardVersionVectorMap());
    }

    /// <inheritdoc/>
    public IServiceScope CreateScope([DisallowNull] string replicaId, ShardVersionVectorMap shardVersionVectors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);
        ArgumentNullException.ThrowIfNull(shardVersionVectors);

        var scope = serviceProvider.CreateScope();
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        
        replicaContext.ReplicaId = replicaId;
        replicaContext.ShardVersionVectors = shardVersionVectors;

        return scope;
    }
}