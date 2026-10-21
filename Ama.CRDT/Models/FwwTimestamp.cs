namespace Ama.CRDT.Models;

using System;

/// <summary>
/// A data structure that bundles a logical timestamp with the causal identity (replica and clock) of the operation
/// specifically configured for First-Writer-Wins (FWW) conflict resolution.
/// </summary>
public readonly record struct FwwTimestamp(CrdtTimestamp Timestamp, string ReplicaId, long Clock) : IComparable<FwwTimestamp>, ICrdtMetadataState
{
    /// <inheritdoc/>
    public int CompareTo(FwwTimestamp other)
    {
        return Timestamp.CompareTo(other.Timestamp);
    }

    /// <inheritdoc/>
    public ICrdtMetadataState DeepClone() => this;

    /// <inheritdoc/>
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not FwwTimestamp otherTs) return this;
        // Keep the earliest timestamp
        return this.CompareTo(otherTs) <= 0 ? this : otherTs;
    }

    /// <inheritdoc/>
    public bool Equals(ICrdtMetadataState? other) => other is FwwTimestamp otherTs && this.Equals(otherTs);
}