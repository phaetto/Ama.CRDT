namespace Modern.CRDT.Attributes;

using Modern.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to explicitly mark an array or list property to use the Longest Common Subsequence (LCS) based diffing strategy.
/// This strategy is typically the default for collections but can be specified for clarity.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CrdtArrayLcsStrategyAttribute : CrdtStrategyAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtArrayLcsStrategyAttribute"/> class.
    /// </summary>
    public CrdtArrayLcsStrategyAttribute() : base(typeof(ArrayLcsStrategy))
    {
    }
}