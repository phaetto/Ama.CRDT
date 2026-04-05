namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the tracking state for the Counter Map strategy.
/// </summary>
/// <param name="Keys">A dictionary mapping map keys to their inner PN-Counter states.</param>
public sealed record CounterMapState(IDictionary<object, PnCounterState> Keys) : IEquatable<CounterMapState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        var cloned = new Dictionary<object, PnCounterState>((Keys as Dictionary<object, PnCounterState>)?.Comparer);
        foreach (var kvp in Keys)
        {
            cloned[kvp.Key] = (PnCounterState)kvp.Value.DeepClone();
        }
        return new CounterMapState(cloned);
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not CounterMapState otherState) return this;
        var merged = new Dictionary<object, PnCounterState>(Keys, (Keys as Dictionary<object, PnCounterState>)?.Comparer);
        foreach (var kvp in otherState.Keys)
        {
            if (merged.TryGetValue(kvp.Key, out var existing))
            {
                merged[kvp.Key] = (PnCounterState)existing.Merge(kvp.Value);
            }
            else
            {
                merged[kvp.Key] = (PnCounterState)kvp.Value.DeepClone();
            }
        }
        return new CounterMapState(merged);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is CounterMapState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(CounterMapState? other)
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