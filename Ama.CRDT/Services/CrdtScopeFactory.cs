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
        return CreateScope(replicaId, new DottedVersionVector());
    }

    /// <inheritdoc/>
    public IServiceScope CreateScope([DisallowNull] string replicaId, DottedVersionVector globalVersionVector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);
        ArgumentNullException.ThrowIfNull(globalVersionVector);

        var scope = serviceProvider.CreateScope();
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        
        replicaContext.ReplicaId = replicaId;
        replicaContext.GlobalVersionVector = globalVersionVector;

        return scope;
    }
}