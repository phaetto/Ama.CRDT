namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents the causality gap for a specific origin replica between a target and a source.
/// </summary>
public readonly record struct OriginSyncRequirement : IEquatable<OriginSyncRequirement>
{
    /// <summary>
    /// Gets the contiguous version the target currently has. 
    /// The target needs operations with a version greater than this, up to the source's contiguous version.
    /// </summary>
    public long TargetContiguousVersion { get; init; }

    /// <summary>
    /// Gets the contiguous version the source currently has.
    /// </summary>
    public long SourceContiguousVersion { get; init; }

    /// <summary>
    /// Gets specific out-of-order versions (dots) that the target already possesses that fall between the TargetContiguousVersion and SourceContiguousVersion.
    /// The source should exclude these when sending the contiguous range.
    /// </summary>
    public IReadOnlySet<long> TargetKnownDots { get; init; }

    /// <summary>
    /// Gets specific out-of-order versions (dots) that the source has, which are missing from the target.
    /// </summary>
    public IReadOnlySet<long> SourceMissingDots { get; init; }

    /// <summary>
    /// Gets a value indicating whether there is actually any missing data for this origin.
    /// </summary>
    public bool HasMissingData => SourceContiguousVersion > TargetContiguousVersion || (SourceMissingDots != null && SourceMissingDots.Count > 0);

    /// <inheritdoc/>
    public bool Equals(OriginSyncRequirement other)
    {
        if (TargetContiguousVersion != other.TargetContiguousVersion) return false;
        if (SourceContiguousVersion != other.SourceContiguousVersion) return false;

        if (TargetKnownDots == null && other.TargetKnownDots != null) return false;
        if (TargetKnownDots != null && other.TargetKnownDots == null) return false;
        if (TargetKnownDots != null && other.TargetKnownDots != null)
        {
            if (TargetKnownDots.Count != other.TargetKnownDots.Count || !TargetKnownDots.All(other.TargetKnownDots.Contains)) return false;
        }

        if (SourceMissingDots == null && other.SourceMissingDots != null) return false;
        if (SourceMissingDots != null && other.SourceMissingDots == null) return false;
        if (SourceMissingDots != null && other.SourceMissingDots != null)
        {
            if (SourceMissingDots.Count != other.SourceMissingDots.Count || !SourceMissingDots.All(other.SourceMissingDots.Contains)) return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TargetContiguousVersion);
        hash.Add(SourceContiguousVersion);
        
        if (TargetKnownDots != null)
        {
            foreach (var dot in TargetKnownDots.OrderBy(x => x)) hash.Add(dot);
        }

        if (SourceMissingDots != null)
        {
            foreach (var dot in SourceMissingDots.OrderBy(x => x)) hash.Add(dot);
        }

        return hash.ToHashCode();
    }
}