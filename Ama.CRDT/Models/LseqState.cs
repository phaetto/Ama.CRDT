namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents the tracking state for the LSEQ strategy.
/// </summary>
/// <param name="Trackers">A list of LSEQ items tracking the dense order.</param>
public sealed record LseqState(List<LseqItem> Trackers) : IEquatable<LseqState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone() => new LseqState(new List<LseqItem>(Trackers));

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not LseqState otherState) return this;
        return new LseqState(new List<LseqItem>(otherState.Trackers));
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is LseqState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(LseqState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Trackers.SequenceEqual(other.Trackers);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var item in Trackers)
        {
            hash ^= item.GetHashCode();
        }
        return hash;
    }
}