namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using Ama.CRDT.Models.Collections;

/// <summary>
/// Represents the tracking state for the Last-Writer-Wins Map (LWW-Map) strategy.
/// </summary>
/// <param name="Keys">A dictionary mapping map keys to their latest causal timestamp.</param>
public sealed record LwwMapState(IDictionary<object, CausalTimestamp> Keys) : IEquatable<LwwMapState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        return new LwwMapState(new CompactDictionary<object, CausalTimestamp>(Keys));
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not LwwMapState otherState) return this;
        var merged = new CompactDictionary<object, CausalTimestamp>(Keys);
        foreach (var kvp in otherState.Keys)
        {
            if (!merged.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        return new LwwMapState(merged);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is LwwMapState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(LwwMapState? other)
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