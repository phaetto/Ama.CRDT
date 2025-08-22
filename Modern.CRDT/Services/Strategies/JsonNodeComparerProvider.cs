namespace Modern.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Provides the appropriate <see cref="IEqualityComparer{JsonNode}"/> for a given element type
/// by searching through registered <see cref="IJsonNodeComparer"/> instances.
/// </summary>
public sealed class JsonNodeComparerProvider : IJsonNodeComparerProvider
{
    private readonly IEnumerable<IJsonNodeComparer> comparers;
    private readonly IEqualityComparer<JsonNode> defaultComparer = JsonNodeDeepEqualityComparer.Instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNodeComparerProvider"/> class.
    /// </summary>
    /// <param name="comparers">A collection of registered type-specific comparers.</param>
    public JsonNodeComparerProvider(IEnumerable<IJsonNodeComparer> comparers)
    {
        this.comparers = comparers ?? Enumerable.Empty<IJsonNodeComparer>();
    }

    /// <summary>
    /// Gets the first registered comparer that supports the given element type,
    /// or falls back to the default deep equality comparer.
    /// </summary>
    /// <param name="elementType">The type of the elements in the array to compare.</param>
    /// <returns>An appropriate <see cref="IEqualityComparer{JsonNode}"/>.</returns>
    public IEqualityComparer<JsonNode> GetComparer(Type elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);

        return comparers.FirstOrDefault(c => c.CanCompare(elementType)) ?? defaultComparer;
    }

    internal sealed class JsonNodeDeepEqualityComparer : IEqualityComparer<JsonNode>
    {
        public static readonly JsonNodeDeepEqualityComparer Instance = new();

        public bool Equals(JsonNode? x, JsonNode? y)
        {
            return JsonNode.DeepEquals(x, y);
        }

        public int GetHashCode(JsonNode obj)
        {
            return obj.GetHashCode();
        }
    }
}