namespace Ama.CRDT.Services.Providers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>
/// Defines a registry that holds the configuration of CRDT strategies and decorators for C# properties,
/// allowing for non-invasive decoration of POCOs.
/// </summary>
public interface ICrdtModelRegistry
{
    /// <summary>
    /// Attempts to retrieve the explicitly configured base strategy type for the given property.
    /// </summary>
    /// <param name="propertyInfo">The property to look up.</param>
    /// <param name="strategyType">When this method returns, contains the configured strategy type, if found.</param>
    /// <returns>True if a strategy is configured for the property; otherwise, false.</returns>
    bool TryGetStrategy(PropertyInfo propertyInfo, [NotNullWhen(true)] out Type? strategyType);

    /// <summary>
    /// Attempts to retrieve the explicitly configured decorator strategy types for the given property.
    /// </summary>
    /// <param name="propertyInfo">The property to look up.</param>
    /// <param name="decoratorTypes">When this method returns, contains the configured decorator types, if found.</param>
    /// <returns>True if decorators are configured for the property; otherwise, false.</returns>
    bool TryGetDecorators(PropertyInfo propertyInfo, [NotNullWhen(true)] out IReadOnlyList<Type>? decoratorTypes);
}