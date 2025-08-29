namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to explicitly mark a collection property to use the Sorted Set strategy.
/// This strategy uses a Longest Common Subsequence (LCS) algorithm for diffing
/// and ensures the collection remains sorted based on a natural or specified order.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CrdtSortedSetStrategyAttribute : CrdtStrategyAttribute
{
    /// <summary>
    /// Gets the name of the property to use for sorting the elements in the set.
    /// If not specified, the strategy will attempt to use a property named 'Id', the element's natural order if it implements <see cref="IComparable"/>, or a composite key of all properties.
    /// </summary>
    public string? SortPropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtSortedSetStrategyAttribute"/> class.
    /// </summary>
    public CrdtSortedSetStrategyAttribute() : base(typeof(SortedSetStrategy))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtSortedSetStrategyAttribute"/> class.
    /// </summary>
    /// <param name="sortPropertyName">The name of the property on the element type to use for sorting.</param>
    public CrdtSortedSetStrategyAttribute(string sortPropertyName) : base(typeof(SortedSetStrategy))
    {
        SortPropertyName = sortPropertyName;
    }
}