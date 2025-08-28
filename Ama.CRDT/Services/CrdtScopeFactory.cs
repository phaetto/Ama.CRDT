namespace Ama.CRDT.Services;

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

/// <inheritdoc/>
internal sealed class CrdtScopeFactory(IServiceProvider serviceProvider) : ICrdtScopeFactory
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

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