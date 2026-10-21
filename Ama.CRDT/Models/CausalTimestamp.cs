namespace Ama.CRDT.Models;

using System;

/// <summary>
/// A data structure that bundles a logical timestamp with the causal identity (replica and clock) of the operation.
/// Used to track metadata for tombstones and deletions safely for garbage collection.
/// </summary>
public readonly record struct CausalTimestamp(CrdtTimestamp Timestamp, string ReplicaId, long Clock) : IComparable<CausalTimestamp>, ICrdtMetadataState
{
    /// <inheritdoc/>
    public int CompareTo(CausalTimestamp other)
    {
        return Timestamp.CompareTo(other.Timestamp);
    }

    /// <inheritdoc/>
    public ICrdtMetadataState DeepClone() => this;

    /// <inheritdoc/>
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not CausalTimestamp otherTs) return this;
        return this.CompareTo(otherTs) >= 0 ? this : otherTs;
    }

    /// <inheritdoc/>
    public bool Equals(ICrdtMetadataState? other) => other is CausalTimestamp otherTs && this.Equals(otherTs);
}