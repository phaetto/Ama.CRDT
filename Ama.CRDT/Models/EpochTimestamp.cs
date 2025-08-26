namespace Ama.CRDT.Models;

/// <summary>
/// A default, backward-compatible implementation of <see cref="ICrdtTimestamp"/> that wraps a <see langword="long"/> value representing Unix milliseconds.
/// </summary>
/// <param name="Value">The number of milliseconds since the Unix epoch.</param>
internal readonly record struct EpochTimestamp(long Value) : ICrdtTimestamp
{
    /// <summary>
    /// Represents the earliest possible timestamp (0 milliseconds since epoch).
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