namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents the positional tracker state for the Array LCS strategy.
/// </summary>
/// <param name="Trackers">A list of positional identifiers tracking the current layout.</param>
public sealed record PositionalState(List<PositionalIdentifier> Trackers) : IEquatable<PositionalState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone() => new PositionalState(new List<PositionalIdentifier>(Trackers));

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not PositionalState otherState) return this;
        return new PositionalState(new List<PositionalIdentifier>(otherState.Trackers));
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is PositionalState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(PositionalState? other)
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