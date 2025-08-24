namespace Modern.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;
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
        var arrayLcsStrategy = new ArrayLcsStrategy(comparerProvider, timestampProvider, options);

        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayLcsStrategy };

        var strategyManager = new CrdtStrategyManager(strategies);

        return new CrdtPatcher(strategyManager);
    }
}