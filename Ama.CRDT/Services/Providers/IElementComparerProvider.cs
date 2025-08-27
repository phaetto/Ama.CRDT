namespace Ama.CRDT.Services.Providers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines a contract for a service that provides the appropriate
/// <see cref="IEqualityComparer{Object}"/> for a given collection element type.
/// </summary>
public interface IElementComparerProvider
{
    /// <summary>
    /// Gets the equality comparer for the specified element type.
    /// </summary>
    /// <param name="elementType">The type of the elements in the collection to compare.</param>
    /// <returns>An appropriate <see cref="IEqualityComparer{Object}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="elementType"/> is null.</exception>
    IEqualityComparer<object> GetComparer([DisallowNull] Type elementType);
}