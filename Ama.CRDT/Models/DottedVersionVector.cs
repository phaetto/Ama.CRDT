namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a Dotted Version Vector (DVV) to track causality and operations seen by a replica.
/// Combines a contiguous version vector with discrete "dots" for out-of-order events.
/// </summary>
public sealed class DottedVersionVector : IEquatable<DottedVersionVector>
{
    /// <summary>
    /// Gets the contiguous maximum versions seen per replica.
    /// </summary>
    public IDictionary<string, long> Versions { get; }

    /// <summary>
    /// Gets the out-of-order isolated versions (dots) seen per replica.
    /// </summary>
    public IDictionary<string, ISet<long>> Dots { get; }

    /// <summary>
    /// Initializes a new, empty <see cref="DottedVersionVector"/>.
    /// </summary>
    public DottedVersionVector()
    {
        Versions = new Dictionary<string, long>();
        Dots = new Dictionary<string, ISet<long>>();
    }

    /// <summary>
    /// Initializes a new <see cref="DottedVersionVector"/> with the specified state.
    /// </summary>
    /// <param name="versions">The contiguous versions state.</param>
    /// <param name="dots">The isolated dots state.</param>
    [JsonConstructor]
    public DottedVersionVector(IDictionary<string, long> versions, IDictionary<string, ISet<long>> dots)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(dots);

        Versions = new Dictionary<string, long>(versions);
        Dots = new Dictionary<string, ISet<long>>();
        
        foreach (var kvp in dots)
        {
            Dots[kvp.Key] = new HashSet<long>(kvp.Value);
        }
    }

    /// <summary>
    /// Checks whether the vector includes the specified version from the given replica.
    /// </summary>
    /// <param name="replicaId">The identifier of the replica.</param>
    /// <param name="version">The logical sequence number or version.</param>
    /// <returns>True if the version has been seen; otherwise, false.</returns>
    public bool Includes(string replicaId, long version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        if (Versions.TryGetValue(replicaId, out var maxContiguous) && version <= maxContiguous)
        {
            return true;
        }

        if (Dots.TryGetValue(replicaId, out var replicaDots) && replicaDots.Contains(version))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Records a version as seen for the specified replica, updating contiguous versions and compacting dots as needed.
    /// </summary>
    /// <param name="replicaId">The identifier of the replica.</param>
    /// <param name="version">The logical sequence number or version.</param>
    public void Add(string replicaId, long version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        Versions.TryGetValue(replicaId, out var currentMax);

        if (version <= currentMax)
        {
            return;
        }

        if (version == currentMax + 1)
        {
            Versions[replicaId] = version;
            
            if (Dots.TryGetValue(replicaId, out var replicaDots))
            {
                long next = version + 1;
                while (replicaDots.Remove(next))
                {
                    Versions[replicaId] = next;
                    next++;
                }

                if (replicaDots.Count == 0)
                {
                    Dots.Remove(replicaId);
                }
            }
        }
        else
        {
            if (!Dots.TryGetValue(replicaId, out var replicaDots))
            {
                replicaDots = new HashSet<long>();
                Dots[replicaId] = replicaDots;
            }
            replicaDots.Add(version);
        }
    }

    /// <summary>
    /// Merges the state of another <see cref="DottedVersionVector"/> into this instance.
    /// </summary>
    /// <param name="other">The other vector to merge.</param>
    public void Merge(DottedVersionVector other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var kvp in other.Versions)
        {
            var replicaId = kvp.Key;
            var otherMax = kvp.Value;

            Versions.TryGetValue(replicaId, out var currentMax);

            if (otherMax > currentMax)
            {
                for (long v = currentMax + 1; v <= otherMax; v++)
                {
                    Add(replicaId, v);
                }
            }
        }

        foreach (var kvp in other.Dots)
        {
            var replicaId = kvp.Key;
            foreach (var dot in kvp.Value)
            {
                Add(replicaId, dot);
            }
        }
    }

    /// <inheritdoc/>
    public bool Equals(DottedVersionVector? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Versions.Count != other.Versions.Count) return false;
        foreach (var kvp in Versions)
        {
            if (!other.Versions.TryGetValue(kvp.Key, out var otherValue) || kvp.Value != otherValue)
            {
                return false;
            }
        }

        if (Dots.Count != other.Dots.Count) return false;
        foreach (var kvp in Dots)
        {
            if (!other.Dots.TryGetValue(kvp.Key, out var otherSet)) return false;
            if (!kvp.Value.SetEquals(otherSet)) return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DottedVersionVector);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in Versions.OrderBy(k => k.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        foreach (var kvp in Dots.OrderBy(k => k.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value.Count);
        }
        return hash.ToHashCode();
    }
}