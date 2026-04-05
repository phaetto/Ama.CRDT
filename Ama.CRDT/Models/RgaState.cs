namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents the tracking state for the RGA strategy.
/// </summary>
/// <param name="Trackers">A list of RGA items tracking the insertion causality and tombstones.</param>
public sealed record RgaState(List<RgaItem> Trackers) : IEquatable<RgaState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone() => new RgaState(new List<RgaItem>(Trackers));

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not RgaState otherState) return this;
        
        var mergedItemsDict = Trackers.ToDictionary(x => x.Identifier);
        foreach (var item in otherState.Trackers)
        {
            if (!mergedItemsDict.TryGetValue(item.Identifier, out var eItem) || (!eItem.IsDeleted && item.IsDeleted))
            {
                mergedItemsDict[item.Identifier] = item;
            }
        }
        var mergedItems = mergedItemsDict.Values.ToList();
        mergedItems.Sort((a, b) => a.Identifier.CompareTo(b.Identifier));
        
        return new RgaState(mergedItems);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is RgaState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(RgaState? other)
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