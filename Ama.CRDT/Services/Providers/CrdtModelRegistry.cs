namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <inheritdoc/>
internal sealed class CrdtModelRegistry : ICrdtModelRegistry
{
    private readonly IReadOnlyDictionary<CrdtPropertyKey, Type> strategies;
    private readonly IReadOnlyDictionary<CrdtPropertyKey, IReadOnlyList<Type>> decorators;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtModelRegistry"/> class.
    /// </summary>
    public CrdtModelRegistry(
        IReadOnlyDictionary<CrdtPropertyKey, Type> strategies, 
        IReadOnlyDictionary<CrdtPropertyKey, IReadOnlyList<Type>> decorators)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(decorators);

        this.strategies = strategies;
        this.decorators = decorators;
    }

    /// <inheritdoc/>
    public bool TryGetStrategy(CrdtPropertyKey propertyKey, [NotNullWhen(true)] out Type? strategyType)
    {
        return this.strategies.TryGetValue(propertyKey, out strategyType);
    }

    /// <inheritdoc/>
    public bool TryGetDecorators(CrdtPropertyKey propertyKey, [NotNullWhen(true)] out IReadOnlyList<Type>? decoratorTypes)
    {
        return this.decorators.TryGetValue(propertyKey, out decoratorTypes);
    }
}