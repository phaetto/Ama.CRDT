namespace Modern.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

/// <summary>
/// Defines a contract for a service that provides the appropriate
/// <see cref="IEqualityComparer{JsonNode}"/> for a given element type.
/// </summary>
public interface IJsonNodeComparerProvider
{
    /// <summary>
    /// Gets the equality comparer for the specified element type.
    /// </summary>
    /// <param name="elementType">The type of the elements in the array to compare.</param>
    /// <returns>An appropriate <see cref="IEqualityComparer{JsonNode}"/>.</returns>
    IEqualityComparer<JsonNode> GetComparer(Type elementType);
}