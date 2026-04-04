namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the tracking state for the Approval Quorum decorator strategy.
/// </summary>
/// <param name="Approvals">A dictionary mapping the proposed payload to a set of Replica IDs that approved it.</param>
public sealed record QuorumState(IDictionary<object, ISet<string>> Approvals) : IEquatable<QuorumState>, ICrdtMetadataState
{
    /// <inheritdoc />
    public ICrdtMetadataState DeepClone()
    {
        var cloned = new Dictionary<object, ISet<string>>((Approvals as Dictionary<object, ISet<string>>)?.Comparer);
        foreach (var kvp in Approvals)
        {
            cloned[kvp.Key] = new HashSet<string>(kvp.Value);
        }
        return new QuorumState(cloned);
    }

    /// <inheritdoc />
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not QuorumState otherState) return this;
        var merged = new Dictionary<object, ISet<string>>(Approvals, (Approvals as Dictionary<object, ISet<string>>)?.Comparer);
        foreach (var kvp in otherState.Approvals)
        {
            if (!merged.TryGetValue(kvp.Key, out var existingSet))
            {
                merged[kvp.Key] = existingSet = new HashSet<string>();
            }
            foreach (var voter in kvp.Value)
            {
                existingSet.Add(voter);
            }
        }
        return new QuorumState(merged);
    }

    /// <inheritdoc />
    public bool Equals(ICrdtMetadataState? other) => other is QuorumState s && Equals(s);

    /// <inheritdoc />
    public bool Equals(QuorumState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Approvals.Count != other.Approvals.Count) return false;
        foreach (var (key, value) in Approvals)
        {
            if (!other.Approvals.TryGetValue(key, out var rightValue) || value is null || rightValue is null || !value.SetEquals(rightValue))
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var (key, value) in Approvals)
        {
            int innerHash = 0;
            if (value is not null)
            {
                foreach (var voter in value)
                {
                    innerHash ^= voter?.GetHashCode() ?? 0;
                }
            }
            hash ^= HashCode.Combine(key, innerHash);
        }
        return hash;
    }
}