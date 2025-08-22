namespace Modern.CRDT.Models;

/// <summary>
/// A default, backward-compatible implementation of ICrdtTimestamp that wraps a long value representing Unix milliseconds.
/// </summary>
public readonly record struct EpochTimestamp(long Value) : ICrdtTimestamp
{
    /// <summary>
    /// Represents the earliest possible timestamp.
    /// </summary>
    public static readonly EpochTimestamp MinValue = new(0);

    /// <inheritdoc/>
    public int CompareTo(ICrdtTimestamp? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (other is not EpochTimestamp otherTimestamp)
        {
            throw new ArgumentException("Cannot compare EpochTimestamp with a different ICrdtTimestamp implementation.", nameof(other));
        }

        return Value.CompareTo(otherTimestamp.Value);
    }
}