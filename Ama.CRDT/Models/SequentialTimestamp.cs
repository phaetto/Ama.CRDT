namespace Ama.CRDT.Models;

using System;

/// <summary>
/// An implementation of <see cref="ICrdtTimestamp"/> that wraps a simple sequential <see langword="long"/> value.
/// This is primarily intended for testing or scenarios where a simple, monotonically increasing counter is sufficient.
/// </summary>
/// <param name="Value">The sequential value of the timestamp.</param>
public readonly record struct SequentialTimestamp(long Value) : ICrdtTimestamp
{
    /// <summary>
    /// Represents the earliest possible timestamp (value 0).
    /// </summary>
    public static readonly SequentialTimestamp MinValue = new(0);

    /// <inheritdoc/>
    public int CompareTo(ICrdtTimestamp? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (other is not SequentialTimestamp otherTimestamp)
        {
            throw new ArgumentException("Cannot compare SequentialTimestamp with a different ICrdtTimestamp implementation.", nameof(other));
        }

        return Value.CompareTo(otherTimestamp.Value);
    }
}