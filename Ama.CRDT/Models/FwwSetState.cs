namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for a First-Writer-Wins Set (FWW-Set).
/// </summary>
/// <param name="Adds">A dictionary mapping added elements to their earliest timestamps.</param>
/// <param name="Removes">A dictionary mapping removed elements to their earliest causal timestamps.</param>
public sealed record FwwSetState(IDictionary<object, ICrdtTimestamp> Adds, IDictionary<object, CausalTimestamp> Removes) : IEquatable<FwwSetState>, ICrdtMetadataState
{
    private static bool DictionaryEquals<TKey, TValue>(IDictionary<TKey, TValue> left, IDictionary<TKey, TValue> right) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue) || !EqualityComparer<TValue>.Default.Equals(value, rightValue))
                return false;
        }
        return true;
    }

    private static int GetDictionaryHashCode<TKey, TValue>(IDictionary<TKey, TValue> dict) where TKey : notnull
    {
        if (dict is null) return 0;
        int hash = 0;
        foreach (var (key, value) in dict)
        {
            hash ^= HashCode.Combine(key, value);
        }
        return hash;
    }

    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        var newAdds = new Dictionary<object, ICrdtTimestamp>(Adds, (Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer);
        var newRemoves = new Dictionary<object, CausalTimestamp>(Removes, (Removes as Dictionary<object, CausalTimestamp>)?.Comparer);
        return new FwwSetState(newAdds, newRemoves);
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not FwwSetState otherState) return this;

        var mergedAdds = new Dictionary<object, ICrdtTimestamp>(Adds, (Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer);
        foreach (var kvp in otherState.Adds)
        {
            if (!mergedAdds.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) < 0)
            {
                mergedAdds[kvp.Key] = kvp.Value;
            }
        }

        var mergedRemoves = new Dictionary<object, CausalTimestamp>(Removes, (Removes as Dictionary<object, CausalTimestamp>)?.Comparer);
        foreach (var kvp in otherState.Removes)
        {
            if (!mergedRemoves.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) < 0)
            {
                mergedRemoves[kvp.Key] = kvp.Value;
            }
        }

        return new FwwSetState(mergedAdds, mergedRemoves);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is FwwSetState otherState && this.Equals(otherState);

    /// <inheritdoc />
    public bool Equals(FwwSetState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return DictionaryEquals(Adds, other.Adds) && DictionaryEquals(Removes, other.Removes);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetDictionaryHashCode(Adds), GetDictionaryHashCode(Removes));
    }
}