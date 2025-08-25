namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to mark a collection property to be managed as a fixed-size array.
/// Each element in the array is treated as an LWW-Register. The size of the array is immutable.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtFixedSizeArrayStrategyAttribute(int size) : CrdtStrategyAttribute(typeof(FixedSizeArrayStrategy))
{
    /// <summary>
    /// Gets the fixed size of the array.
    /// </summary>
    public int Size { get; } = size;
}