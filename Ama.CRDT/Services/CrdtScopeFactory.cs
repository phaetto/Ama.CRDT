namespace Ama.CRDT.Services;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        var scope = serviceProvider.CreateScope();
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        
        replicaContext.ReplicaId = replicaId;

        return scope;
    }
}