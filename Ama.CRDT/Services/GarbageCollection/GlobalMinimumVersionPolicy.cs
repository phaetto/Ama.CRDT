namespace Ama.CRDT.Services.GarbageCollection;

using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Models;

/// <summary>
/// A mathematically safe compaction policy based on the Global Minimum Version Vector (GMVV).
/// It ensures that a causal operation is only compacted if every known replica in the cluster has acknowledged it.
/// </summary>
public sealed class GlobalMinimumVersionPolicy : ICompactionPolicy
{
    private readonly IReadOnlyDictionary<string, long> _globalMinimumVersions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalMinimumVersionPolicy"/> class.
    /// </summary>
    /// <param name="globalMinimumVersions">A dictionary mapping each origin replica ID to the lowest contiguous version acknowledged by all replicas in the cluster.</param>
    public GlobalMinimumVersionPolicy(IReadOnlyDictionary<string, long> globalMinimumVersions)
    {
        _globalMinimumVersions = globalMinimumVersions ?? throw new ArgumentNullException(nameof(globalMinimumVersions));
    }

    /// <summary>
    /// Calculates the Global Minimum Version Vector from a collection of replica version vectors.
    /// </summary>
    /// <param name="clusterVectors">The version vectors of all active replicas in the cluster.</param>
    /// <returns>A new <see cref="GlobalMinimumVersionPolicy"/> configured with the calculated global minimums.</returns>
    public static GlobalMinimumVersionPolicy CreateFromClusterState(IEnumerable<DottedVersionVector> clusterVectors)
    {
        ArgumentNullException.ThrowIfNull(clusterVectors);

        var vectors = clusterVectors.ToList();
        if (vectors.Count == 0)
        {
            return new GlobalMinimumVersionPolicy(new Dictionary<string, long>());
        }

        // Find all unique origin replicas across all vectors
        var allOrigins = vectors.SelectMany(v => v.Versions.Keys).Distinct().ToList();
        var gmvv = new Dictionary<string, long>();

        foreach (var origin in allOrigins)
        {
            // The global minimum for an origin is the lowest contiguous version seen by ANY replica in the cluster.
            long minVersion = long.MaxValue;
            foreach (var vector in vectors)
            {
                if (vector.Versions.TryGetValue(origin, out var version))
                {
                    if (version < minVersion)
                    {
                        minVersion = version;
                    }
                }
                else
                {
                    // One replica hasn't seen anything from this origin yet, so GMVV is effectively 0
                    minVersion = 0;
                    break;
                }
            }

            if (minVersion > 0 && minVersion != long.MaxValue)
            {
                gmvv[origin] = minVersion;
            }
        }

        return new GlobalMinimumVersionPolicy(gmvv);
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(ICrdtTimestamp timestamp)
    {
        // GMVV tracks causal sequence numbers, not wall-clock timestamps.
        // Therefore, it cannot safely determine if a purely time-based tombstone (like LWW) is safe to delete.
        // A ThresholdCompactionPolicy or CompositeCompactionPolicy should be used to handle ICrdtTimestamp.
        return false;
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(string replicaId, long version)
    {
        if (string.IsNullOrWhiteSpace(replicaId))
        {
            return false;
        }

        if (_globalMinimumVersions.TryGetValue(replicaId, out var minVersion))
        {
            return version <= minVersion;
        }

        return false;
    }
}