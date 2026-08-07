namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for a numeric property managed by a G-Counter (GCounterStrategy).
/// It tracks the total positive increments contributed by each replica.
/// </summary>
/// <param name="Contributions">A dictionary mapping replica IDs to their total contributed increments.</param>
public sealed record GCounterState(IDictionary<string, decimal> Contributions) : IEquatable<GCounterState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        return new GCounterState(new Dictionary<string, decimal>(Contributions));
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not GCounterState otherState) return this;

        var merged = new Dictionary<string, decimal>(Contributions);
        foreach (var kvp in otherState.Contributions)
        {
            if (!merged.TryGetValue(kvp.Key, out var existing) || kvp.Value > existing)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return new GCounterState(merged);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is GCounterState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(GCounterState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Contributions.Count != other.Contributions.Count) return false;

        foreach (var (key, value) in Contributions)
        {
            if (!other.Contributions.TryGetValue(key, out var otherValue) || value != otherValue)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var (key, value) in Contributions)
        {
            hash ^= HashCode.Combine(key, value);
        }
        return hash;
    }
}