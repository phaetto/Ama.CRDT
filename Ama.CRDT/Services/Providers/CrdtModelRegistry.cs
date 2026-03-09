namespace Ama.CRDT.Services.Providers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <inheritdoc/>
internal sealed class CrdtModelRegistry : ICrdtModelRegistry
{
    private readonly IReadOnlyDictionary<PropertyInfo, Type> strategies;
    private readonly IReadOnlyDictionary<PropertyInfo, IReadOnlyList<Type>> decorators;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtModelRegistry"/> class.
    /// </summary>
    public CrdtModelRegistry(
        IReadOnlyDictionary<PropertyInfo, Type> strategies, 
        IReadOnlyDictionary<PropertyInfo, IReadOnlyList<Type>> decorators)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(decorators);

        this.strategies = strategies;
        this.decorators = decorators;
    }

    /// <inheritdoc/>
    public bool TryGetStrategy(PropertyInfo propertyInfo, [NotNullWhen(true)] out Type? strategyType)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);
        return this.strategies.TryGetValue(propertyInfo, out strategyType);
    }

    /// <inheritdoc/>
    public bool TryGetDecorators(PropertyInfo propertyInfo, [NotNullWhen(true)] out IReadOnlyList<Type>? decoratorTypes)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);
        return this.decorators.TryGetValue(propertyInfo, out decoratorTypes);
    }
}