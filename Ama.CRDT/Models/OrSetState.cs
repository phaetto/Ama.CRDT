namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for an Observed-Remove Set (OR-Set) or OR-Map.
/// </summary>
/// <param name="Adds">A dictionary mapping added elements to a set of unique tags.</param>
/// <param name="Removes">A dictionary mapping removed elements to a dictionary of unique tags and their causal tracking data for GC.</param>
public sealed record OrSetState(IDictionary<object, ISet<Guid>> Adds, IDictionary<object, IDictionary<Guid, CausalTimestamp>> Removes) : IEquatable<OrSetState>, ICrdtMetadataState
{
    private static bool DictionaryOfSetsEquals(IDictionary<object, ISet<Guid>> left, IDictionary<object, ISet<Guid>> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue)) return false;
            if (ReferenceEquals(value, rightValue)) continue;
            if (value is null || rightValue is null) return false;
            if (!value.SetEquals(rightValue)) return false;
        }
        return true;
    }
    
    private static bool DictionaryOfDictionariesEquals(IDictionary<object, IDictionary<Guid, CausalTimestamp>> left, IDictionary<object, IDictionary<Guid, CausalTimestamp>> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue)) return false;
            if (ReferenceEquals(value, rightValue)) continue;
            if (value is null || rightValue is null) return false;
            if (value.Count != rightValue.Count) return false;
            foreach (var (innerKey, innerValue) in value)
            {
                if (!rightValue.TryGetValue(innerKey, out var rightInnerValue) || !innerValue.Equals(rightInnerValue))
                    return false;
            }
        }
        return true;
    }

    private static int GetDictionaryOfSetsHashCode(IDictionary<object, ISet<Guid>> dict)
    {
        if (dict is null) return 0;
        int hash = 0;
        foreach (var (key, value) in dict)
        {
            int setHash = 0;
            if (value is not null)
            {
                foreach (var item in value)
                {
                    setHash ^= item.GetHashCode();
                }
            }
            hash ^= HashCode.Combine(key, setHash);
        }
        return hash;
    }

    private static int GetDictionaryOfDictionariesHashCode(IDictionary<object, IDictionary<Guid, CausalTimestamp>> dict)
    {
        if (dict is null) return 0;
        int hash = 0;
        foreach (var (key, value) in dict)
        {
            int innerHash = 0;
            if (value is not null)
            {
                foreach (var (innerKey, innerValue) in value)
                {
                    innerHash ^= HashCode.Combine(innerKey, innerValue);
                }
            }
            hash ^= HashCode.Combine(key, innerHash);
        }
        return hash;
    }

    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        var addedComparer = (Adds as Dictionary<object, ISet<Guid>>)?.Comparer;
        var newAdds = new Dictionary<object, ISet<Guid>>(addedComparer);
        foreach (var kvp in Adds) newAdds[kvp.Key] = new HashSet<Guid>(kvp.Value);

        var removedComparer = (Removes as Dictionary<object, IDictionary<Guid, CausalTimestamp>>)?.Comparer;
        var newRemoves = new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(removedComparer);
        foreach (var kvp in Removes) newRemoves[kvp.Key] = new Dictionary<Guid, CausalTimestamp>(kvp.Value);

        return new OrSetState(newAdds, newRemoves);
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not OrSetState otherState) return this;

        var addedComparer = (Adds as Dictionary<object, ISet<Guid>>)?.Comparer;
        var mergedAdds = new Dictionary<object, ISet<Guid>>(addedComparer);
        foreach (var kvp in Adds) mergedAdds[kvp.Key] = new HashSet<Guid>(kvp.Value);
        foreach (var kvp in otherState.Adds)
        {
            if (!mergedAdds.TryGetValue(kvp.Key, out var set)) mergedAdds[kvp.Key] = set = new HashSet<Guid>();
            foreach (var id in kvp.Value) set.Add(id);
        }

        var removedComparer = (Removes as Dictionary<object, IDictionary<Guid, CausalTimestamp>>)?.Comparer;
        var mergedRemoves = new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(removedComparer);
        foreach (var kvp in Removes) mergedRemoves[kvp.Key] = new Dictionary<Guid, CausalTimestamp>(kvp.Value);
        foreach (var kvp in otherState.Removes)
        {
            if (!mergedRemoves.TryGetValue(kvp.Key, out var dict)) mergedRemoves[kvp.Key] = dict = new Dictionary<Guid, CausalTimestamp>();
            foreach (var innerKvp in kvp.Value)
            {
                if (!dict.TryGetValue(innerKvp.Key, out var existing) || innerKvp.Value.CompareTo(existing) > 0)
                {
                    dict[innerKvp.Key] = innerKvp.Value;
                }
            }
        }

        return new OrSetState(mergedAdds, mergedRemoves);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is OrSetState otherState && this.Equals(otherState);

    /// <inheritdoc />
    public bool Equals(OrSetState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return DictionaryOfSetsEquals(Adds, other.Adds) && DictionaryOfDictionariesEquals(Removes, other.Removes);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetDictionaryOfSetsHashCode(Adds), GetDictionaryOfDictionariesHashCode(Removes));
    }
}