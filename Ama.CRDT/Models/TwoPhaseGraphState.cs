namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the state for a Two-Phase Graph (2P-Graph).
/// </summary>
/// <param name="VertexAdds">A set of added vertices.</param>
/// <param name="VertexTombstones">A dictionary of removed vertices (tombstones) tracking the causality of the removal.</param>
/// <param name="EdgeAdds">A set of added edges.</param>
/// <param name="EdgeTombstones">A dictionary of removed edges (tombstones) tracking the causality of the removal.</param>
public sealed record TwoPhaseGraphState(ISet<object> VertexAdds, IDictionary<object, CausalTimestamp> VertexTombstones, ISet<object> EdgeAdds, IDictionary<object, CausalTimestamp> EdgeTombstones) : IEquatable<TwoPhaseGraphState>, ICrdtMetadataState
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
        return new TwoPhaseGraphState(
            new HashSet<object>(VertexAdds, (VertexAdds as HashSet<object>)?.Comparer),
            new Dictionary<object, CausalTimestamp>(VertexTombstones, (VertexTombstones as Dictionary<object, CausalTimestamp>)?.Comparer),
            new HashSet<object>(EdgeAdds, (EdgeAdds as HashSet<object>)?.Comparer),
            new Dictionary<object, CausalTimestamp>(EdgeTombstones, (EdgeTombstones as Dictionary<object, CausalTimestamp>)?.Comparer)
        );
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not TwoPhaseGraphState otherState) return this;

        var mergedVertexAdds = new HashSet<object>(VertexAdds, (VertexAdds as HashSet<object>)?.Comparer);
        foreach (var item in otherState.VertexAdds) mergedVertexAdds.Add(item);

        var mergedVertexTombstones = new Dictionary<object, CausalTimestamp>(VertexTombstones, (VertexTombstones as Dictionary<object, CausalTimestamp>)?.Comparer);
        foreach (var kvp in otherState.VertexTombstones)
        {
            if (!mergedVertexTombstones.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
            {
                mergedVertexTombstones[kvp.Key] = kvp.Value;
            }
        }

        var mergedEdgeAdds = new HashSet<object>(EdgeAdds, (EdgeAdds as HashSet<object>)?.Comparer);
        foreach (var item in otherState.EdgeAdds) mergedEdgeAdds.Add(item);

        var mergedEdgeTombstones = new Dictionary<object, CausalTimestamp>(EdgeTombstones, (EdgeTombstones as Dictionary<object, CausalTimestamp>)?.Comparer);
        foreach (var kvp in otherState.EdgeTombstones)
        {
            if (!mergedEdgeTombstones.TryGetValue(kvp.Key, out var existing) || kvp.Value.CompareTo(existing) > 0)
            {
                mergedEdgeTombstones[kvp.Key] = kvp.Value;
            }
        }

        return new TwoPhaseGraphState(mergedVertexAdds, mergedVertexTombstones, mergedEdgeAdds, mergedEdgeTombstones);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is TwoPhaseGraphState otherState && this.Equals(otherState);

    /// <inheritdoc />
    public bool Equals(TwoPhaseGraphState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return SetEquals(VertexAdds, other.VertexAdds) &&
               DictionaryEquals(VertexTombstones, other.VertexTombstones) &&
               SetEquals(EdgeAdds, other.EdgeAdds) &&
               DictionaryEquals(EdgeTombstones, other.EdgeTombstones);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            GetSetHashCode(VertexAdds),
            GetDictionaryHashCode(VertexTombstones),
            GetSetHashCode(EdgeAdds),
            GetDictionaryHashCode(EdgeTombstones)
        );
    }
}