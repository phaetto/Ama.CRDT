namespace Ama.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines a contract for a type-specific equality comparer for collection elements,
/// used to uniquely identify elements within an array (e.g., by an 'Id' property).
/// </summary>
public interface IElementComparer : IEqualityComparer<object>
{
    /// <summary>
    /// Determines whether this comparer can be used for the specified element type.
    /// </summary>
    /// <param name="type">The type of the element in the collection.</param>
    /// <returns><c>true</c> if this comparer supports the type; otherwise, <c>false</c>.</returns>
    bool CanCompare([DisallowNull] Type type);
}