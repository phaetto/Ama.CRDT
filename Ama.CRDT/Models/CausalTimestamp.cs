namespace Ama.CRDT.Models;

using System;

/// <summary>
/// A data structure that bundles a logical timestamp with the causal identity (replica and clock) of the operation.
/// Used to track metadata for tombstones and deletions safely for garbage collection.
/// </summary>
public readonly record struct CausalTimestamp(ICrdtTimestamp Timestamp, string ReplicaId, long Clock) : IComparable<CausalTimestamp>
{
    /// <inheritdoc/>
    public int CompareTo(CausalTimestamp other)
    {
        if (Timestamp is null && other.Timestamp is null) return 0;
        if (Timestamp is null) return -1;
        if (other.Timestamp is null) return 1;
        return Timestamp.CompareTo(other.Timestamp);
    }
}