namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services.Pooling;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

/// <inheritdoc/>
internal sealed class CrdtScopeFactory : ICrdtScopeFactory
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtStringPool stringPool;

    public CrdtScopeFactory(IServiceProvider serviceProvider, ICrdtStringPool stringPool)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(stringPool);
        this.serviceProvider = serviceProvider;
        this.stringPool = stringPool;
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

        var pooledReplicaId = stringPool.GetOrAdd(replicaId);
        var scope = serviceProvider.CreateScope();
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        
        replicaContext.ReplicaId = pooledReplicaId;
        replicaContext.ShardVersionVectors = shardVersionVectors;

        return scope;
    }
}