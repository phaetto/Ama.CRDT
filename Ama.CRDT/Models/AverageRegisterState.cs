namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for an Average Register.
/// </summary>
/// <param name="Contributions">A dictionary mapping replica IDs to their contributed values.</param>
public sealed record AverageRegisterState(IDictionary<string, AverageRegisterValue> Contributions) : IEquatable<AverageRegisterState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        return new AverageRegisterState(new Dictionary<string, AverageRegisterValue>(Contributions));
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not AverageRegisterState otherState) return this;
        
        var merged = new Dictionary<string, AverageRegisterValue>(Contributions);
        foreach (var kvp in otherState.Contributions)
        {
            if (!merged.TryGetValue(kvp.Key, out var existing) || kvp.Value.Timestamp.CompareTo(existing.Timestamp) > 0)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        return new AverageRegisterState(merged);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is AverageRegisterState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(AverageRegisterState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Contributions.Count != other.Contributions.Count) return false;
        foreach (var (key, value) in Contributions)
        {
            if (!other.Contributions.TryGetValue(key, out var otherValue) || !EqualityComparer<AverageRegisterValue>.Default.Equals(value, otherValue))
                return false;
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