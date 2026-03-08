namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents the data a target replica needs to request from a source replica to catch up with its causal history.
/// </summary>
public readonly record struct ReplicaSyncRequirement : IEquatable<ReplicaSyncRequirement>
{
    /// <summary>
    /// Gets the identifier of the replica that is behind and needs data.
    /// </summary>
    public string TargetReplicaId { get; init; }

    /// <summary>
    /// Gets the identifier of the replica that is ahead and has the data.
    /// </summary>
    public string SourceReplicaId { get; init; }

    /// <summary>
    /// Gets a mapping of origin replica identifiers to their respective synchronization requirements.
    /// </summary>
    public IReadOnlyDictionary<string, OriginSyncRequirement> RequirementsByOrigin { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target needs any data from the source.
    /// </summary>
    public bool IsBehind => RequirementsByOrigin != null && RequirementsByOrigin.Any(x => x.Value.HasMissingData);

    /// <inheritdoc/>
    public bool Equals(ReplicaSyncRequirement other)
    {
        if (TargetReplicaId != other.TargetReplicaId) return false;
        if (SourceReplicaId != other.SourceReplicaId) return false;
        
        if (RequirementsByOrigin == null && other.RequirementsByOrigin != null) return false;
        if (RequirementsByOrigin != null && other.RequirementsByOrigin == null) return false;
        if (RequirementsByOrigin != null && other.RequirementsByOrigin != null)
        {
            if (RequirementsByOrigin.Count != other.RequirementsByOrigin.Count) return false;
            foreach (var kvp in RequirementsByOrigin)
            {
                if (!other.RequirementsByOrigin.TryGetValue(kvp.Key, out var otherReq) || !kvp.Value.Equals(otherReq))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TargetReplicaId);
        hash.Add(SourceReplicaId);
        
        if (RequirementsByOrigin != null)
        {
            foreach (var kvp in RequirementsByOrigin.OrderBy(x => x.Key))
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }
        }

        return hash.ToHashCode();
    }
}