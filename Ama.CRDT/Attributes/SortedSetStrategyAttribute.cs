namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to explicitly mark a collection property to use the Sorted Set strategy.
/// This strategy uses a Longest Common Subsequence (LCS) algorithm for diffing
/// and ensures the collection remains sorted based on a natural or specified order.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SortedSetStrategyAttribute : CrdtStrategyAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SortedSetStrategyAttribute"/> class.
    /// </summary>
    public SortedSetStrategyAttribute() : base(typeof(SortedSetStrategy))
    {
    }
}