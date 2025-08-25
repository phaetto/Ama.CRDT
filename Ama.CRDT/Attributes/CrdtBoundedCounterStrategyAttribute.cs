namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// Specifies that a numeric property should be treated as a Bounded Counter.
/// The counter's value will be clamped within the specified minimum and maximum bounds.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtBoundedCounterStrategyAttribute(long min, long max) : CrdtStrategyAttribute(typeof(BoundedCounterStrategy))
{
    /// <summary>
    /// Gets the minimum allowed value for the counter.
    /// </summary>
    public long Min { get; } = min;

    /// <summary>
    /// Gets the maximum allowed value for the counter.
    /// </summary>
    public long Max { get; } = max;
}