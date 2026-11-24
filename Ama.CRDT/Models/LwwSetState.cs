namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using Ama.CRDT.Models.Collections;

/// <summary>
/// Represents the state for a Last-Writer-Wins Set (LWW-Set) and similar structures.
/// </summary>
/// <param name="Adds">A dictionary mapping added elements to their timestamps.</param>
/// <param name="Removes">A dictionary mapping removed elements to their causal timestamps, tracking who and when removed them for GC.</param>
public sealed record LwwSetState(IDictionary<object, CrdtTimestamp> Adds, IDictionary<object, CausalTimestamp> Removes) : IEquatable<LwwSetState>, ICrdtMetadataState
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
        var addedComparer = (Adds as CompactDictionary<object, CrdtTimestamp>)?.Comparer ?? (Adds as Dictionary<object, CrdtTimestamp>)?.Comparer;
        var removedComparer = (Removes as CompactDictionary<object, CausalTimestamp>)?.Comparer ?? (Removes as Dictionary<object, CausalTimestamp>)?.Comparer;
        var newAdds = new CompactDictionary<object, CrdtTimestamp>(Adds, addedComparer);
        var newRemoves = new CompactDictionary<object, CausalTimestamp>(Removes, removedComparer);
        return new LwwSetState(newAdds, newRemoves);
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not LwwSetState otherState) return this;

        var addedComparer = (Adds as CompactDictionary<object, CrdtTimestamp>)?.Comparer ?? (Adds as Dictionary<object, CrdtTimestamp>)?.Comparer;
        var mergedAdds = new CompactDictionary<object, CrdtTimestamp>(Adds, addedComparer);
        foreach (var kvp in otherState.Adds)
        {
            if (!mergedAdds.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
            {
                mergedAdds[kvp.Key] = kvp.Value;
            }
        }

        var removedComparer = (Removes as CompactDictionary<object, CausalTimestamp>)?.Comparer ?? (Removes as Dictionary<object, CausalTimestamp>)?.Comparer;
        var mergedRemoves = new CompactDictionary<object, CausalTimestamp>(Removes, removedComparer);
        foreach (var kvp in otherState.Removes)
        {
            if (!mergedRemoves.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
            {
                mergedRemoves[kvp.Key] = kvp.Value;
            }
        }

        return new LwwSetState(mergedAdds, mergedRemoves);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is LwwSetState otherState && this.Equals(otherState);

    /// <inheritdoc />
    public bool Equals(LwwSetState? other)
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