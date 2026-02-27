namespace Ama.CRDT.Models;

using System;

/// <summary>
/// Represents a globally unique identifier for a node in the RGA (Replicated Growable Array) CRDT.
/// It uses a logical timestamp (or ticks) and a replica identifier to ensure uniqueness and total ordering.
/// </summary>
public readonly record struct RgaIdentifier(long Timestamp, string ReplicaId) : IComparable<RgaIdentifier>, IComparable
{
    /// <inheritdoc />
    public int CompareTo(RgaIdentifier other)
    {
        var tsComparison = Timestamp.CompareTo(other.Timestamp);
        if (tsComparison != 0) return tsComparison;
        
        return string.Compare(ReplicaId, other.ReplicaId, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        return obj is RgaIdentifier other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(RgaIdentifier)}");
    }
}