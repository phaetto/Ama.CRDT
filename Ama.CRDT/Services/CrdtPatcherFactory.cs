namespace Ama.CRDT.Services;

using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ama.CRDT.Models;
using System;
using System.Diagnostics.CodeAnalysis;

/// <inheritdoc/>
internal sealed class CrdtPatcherFactory(IServiceProvider serviceProvider) : ICrdtPatcherFactory
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc/>
    public ICrdtPatcher Create([DisallowNull] string replicaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        var options = Options.Create(new CrdtOptions { ReplicaId = replicaId });

        var timestampProvider = serviceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var comparerProvider = serviceProvider.GetRequiredService<IElementComparerProvider>();
        var strategies = serviceProvider.GetServices<ICrdtStrategy>();

        var strategyManager = new CrdtStrategyManager(strategies);

        return new CrdtPatcher(strategyManager);
    }
}