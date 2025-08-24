namespace Ama.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;

/// <summary>
/// Defines a contract for a service that provides the appropriate
/// <see cref="IEqualityComparer{Object}"/> for a given element type.
/// </summary>
public interface IElementComparerProvider
{
    /// <summary>
    /// Gets the equality comparer for the specified element type.
    /// </summary>
    /// <param name="elementType">The type of the elements in the array to compare.</param>
    /// <returns>An appropriate <see cref="IEqualityComparer{Object}"/>.</returns>
    IEqualityComparer<object> GetComparer(Type elementType);
}