namespace Ama.CRDT.Services;

using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ama.CRDT.Models;
using System;

internal sealed class CrdtPatcherFactory(IServiceProvider serviceProvider) : ICrdtPatcherFactory
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public ICrdtPatcher Create(string replicaId)
    {
        if (string.IsNullOrWhiteSpace(replicaId))
        {
            throw new ArgumentException("Replica ID cannot be null or whitespace.", nameof(replicaId));
        }

        var options = Options.Create(new CrdtOptions { ReplicaId = replicaId });

        var timestampProvider = serviceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var comparerProvider = serviceProvider.GetRequiredService<IElementComparerProvider>();

        var lwwStrategy = new LwwStrategy(options);
        var counterStrategy = new CounterStrategy(timestampProvider, options);
        var arrayLcsStrategy = new SortedSetStrategy(comparerProvider, timestampProvider, options);

        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayLcsStrategy };

        var strategyManager = new CrdtStrategyManager(strategies);

        return new CrdtPatcher(strategyManager);
    }
}