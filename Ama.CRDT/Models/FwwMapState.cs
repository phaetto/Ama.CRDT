namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the tracking state for the First-Writer-Wins Map (FWW-Map) strategy.
/// </summary>
/// <param name="Keys">A dictionary mapping map keys to their earliest causal timestamp.</param>
public sealed record FwwMapState(IDictionary<object, CausalTimestamp> Keys) : IEquatable<FwwMapState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        return new FwwMapState(new Dictionary<object, CausalTimestamp>(Keys, (Keys as Dictionary<object, CausalTimestamp>)?.Comparer));
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not FwwMapState otherState) return this;
        var merged = new Dictionary<object, CausalTimestamp>(Keys, (Keys as Dictionary<object, CausalTimestamp>)?.Comparer);
        foreach (var kvp in otherState.Keys)
        {
            if (!merged.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) < 0)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        return new FwwMapState(merged);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is FwwMapState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(FwwMapState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Keys.Count != other.Keys.Count) return false;
        foreach (var (key, value) in Keys)
        {
            if (!other.Keys.TryGetValue(key, out var otherValue) || !value.Equals(otherValue))
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var (key, value) in Keys)
        {
            hash ^= HashCode.Combine(key, value);
        }
        return hash;
    }
}