namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for a Two-Phase Set (2P-Set).
/// </summary>
/// <param name="Adds">A set of added elements.</param>
/// <param name="Tombstones">A dictionary of removed elements (tombstones) tracking the causality of the removal.</param>
public sealed record TwoPhaseSetState(ISet<object> Adds, IDictionary<object, CausalTimestamp> Tombstones) : IEquatable<TwoPhaseSetState>, ICrdtMetadataState
{
    private static bool SetEquals(ISet<object> left, ISet<object> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.SetEquals(right);
    }

    private static int GetSetHashCode(ISet<object> set)
    {
        if (set is null) return 0;
        int hash = 0;
        foreach (var item in set)
        {
            hash ^= item?.GetHashCode() ?? 0;
        }
        return hash;
    }

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
        return new TwoPhaseSetState(
            new HashSet<object>(Adds, (Adds as HashSet<object>)?.Comparer),
            new Dictionary<object, CausalTimestamp>(Tombstones, (Tombstones as Dictionary<object, CausalTimestamp>)?.Comparer)
        );
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not TwoPhaseSetState otherState) return this;

        var mergedAdds = new HashSet<object>(Adds, (Adds as HashSet<object>)?.Comparer);
        foreach (var item in otherState.Adds) mergedAdds.Add(item);

        var mergedTombstones = new Dictionary<object, CausalTimestamp>(Tombstones, (Tombstones as Dictionary<object, CausalTimestamp>)?.Comparer);
        foreach (var kvp in otherState.Tombstones)
        {
            if (!mergedTombstones.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
            {
                mergedTombstones[kvp.Key] = kvp.Value;
            }
        }

        return new TwoPhaseSetState(mergedAdds, mergedTombstones);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is TwoPhaseSetState otherState && this.Equals(otherState);

    /// <inheritdoc />
    public bool Equals(TwoPhaseSetState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return SetEquals(Adds, other.Adds) && DictionaryEquals(Tombstones, other.Tombstones);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetSetHashCode(Adds), GetDictionaryHashCode(Tombstones));
    }
}