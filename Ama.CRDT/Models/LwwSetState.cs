namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for a Last-Writer-Wins Set (LWW-Set) and similar structures.
/// </summary>
/// <param name="Adds">A dictionary mapping added elements to their timestamps.</param>
/// <param name="Removes">A dictionary mapping removed elements to their causal timestamps, tracking who and when removed them for GC.</param>
public sealed record LwwSetState(IDictionary<object, ICrdtTimestamp> Adds, IDictionary<object, CausalTimestamp> Removes) : IEquatable<LwwSetState>
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