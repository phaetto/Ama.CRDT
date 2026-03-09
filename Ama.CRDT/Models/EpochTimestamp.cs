namespace Ama.CRDT.Models;

using System;

/// <summary>
/// A default, backward-compatible implementation of <see cref="ICrdtTimestamp"/> that wraps a <see langword="long"/> value representing Unix milliseconds.
/// </summary>
/// <param name="Value">The number of milliseconds since the Unix epoch.</param>
/// <param name="ReplicaId">The identifier of the replica that generated the timestamp, used for deterministic tie-breaking during collisions.</param>
public readonly record struct EpochTimestamp(long Value, string? ReplicaId = null) : ICrdtTimestamp
{
    /// <summary>
    /// Represents the earliest possible timestamp (0 milliseconds since epoch).
    /// </summary>
    public static readonly EpochTimestamp MinValue = new(0, string.Empty);

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

        var timeComparison = Value.CompareTo(otherTimestamp.Value);
        if (timeComparison != 0)
        {
            return timeComparison;
        }

        // Deterministic tie-breaking using the ReplicaId when times collide exactly.
        return string.CompareOrdinal(ReplicaId ?? string.Empty, otherTimestamp.ReplicaId ?? string.Empty);
    }
}