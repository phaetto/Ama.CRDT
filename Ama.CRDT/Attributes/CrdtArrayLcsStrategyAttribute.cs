namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to explicitly mark a collection property to use the Array LCS strategy,
/// which leverages positional identifiers for stable, causally-correct ordering of elements.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CrdtArrayLcsStrategyAttribute : CrdtStrategyAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtArrayLcsStrategyAttribute"/> class,
    /// associating it with the <see cref="ArrayLcsStrategy"/>.
    /// </summary>
    public CrdtArrayLcsStrategyAttribute() : base(typeof(ArrayLcsStrategy))
    {
    }
}