namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for a numeric property managed by a PN-Counter (CounterStrategy).
/// It tracks the positive and negative increments per replica to allow for accurate mathematical state merging.
/// </summary>
/// <param name="Contributions">A dictionary mapping replica IDs to their sum of positive and negative increments.</param>
public sealed record CounterState(IDictionary<string, PnCounterState> Contributions) : IEquatable<CounterState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        return new CounterState(new Dictionary<string, PnCounterState>(Contributions));
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not CounterState otherState) return this;

        var merged = new Dictionary<string, PnCounterState>(Contributions);
        foreach (var kvp in otherState.Contributions)
        {
            if (!merged.TryGetValue(kvp.Key, out var existing))
            {
                merged[kvp.Key] = kvp.Value;
            }
            else
            {
                merged[kvp.Key] = new PnCounterState(
                    Math.Max(existing.P, kvp.Value.P),
                    Math.Max(existing.N, kvp.Value.N));
            }
        }

        return new CounterState(merged);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is CounterState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(CounterState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Contributions.Count != other.Contributions.Count) return false;

        foreach (var (key, value) in Contributions)
        {
            if (!other.Contributions.TryGetValue(key, out var otherValue) || !EqualityComparer<PnCounterState>.Default.Equals(value, otherValue))
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