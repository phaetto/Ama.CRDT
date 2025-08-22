namespace Modern.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

/// <summary>
/// Defines a contract for a type-specific equality comparer for JsonNode instances,
/// intended for use within the ArrayLcsStrategy.
/// </summary>
public interface IJsonNodeComparer : IEqualityComparer<JsonNode>
{
    /// <summary>
    /// Determines whether this comparer can be used for the specified element type.
    /// </summary>
    /// <param name="type">The type of the element in the array.</param>
    /// <returns><c>true</c> if this comparer supports the type; otherwise, <c>false</c>.</returns>
    bool CanCompare(Type type);
}