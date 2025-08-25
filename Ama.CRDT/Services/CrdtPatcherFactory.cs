namespace Ama.CRDT.Services;

using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

/// <inheritdoc/>
internal sealed class CrdtPatcherFactory(IServiceProvider serviceProvider, IEnumerable<ICrdtStrategy> originalStrategies) : ICrdtPatcherFactory
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    
    /// <inheritdoc/>
    public ICrdtPatcher Create([DisallowNull] string replicaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        var newOptions = Options.Create(new CrdtOptions { ReplicaId = replicaId });
        var strategies = new List<ICrdtStrategy>();

        foreach (var type in originalStrategies)
        {
            if (ActivatorUtilities.CreateInstance(serviceProvider, type.GetType(), newOptions) is ICrdtStrategy strategy)
            {
                strategies.Add(strategy);
            }
        }
        
        var strategyManager = new CrdtStrategyManager(strategies);

        return new CrdtPatcher(strategyManager);
    }
}