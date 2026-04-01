namespace Ama.CRDT.Services.Versioning;

using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Models;

/// <summary>
/// Implements <see cref="IVersionVectorSyncService"/> to calculate synchronization requirements between replicas based on their Dotted Version Vectors.
/// </summary>
public sealed class VersionVectorSyncService : IVersionVectorSyncService
{
    /// <inheritdoc/>
    public ReplicaSyncRequirement CalculateRequirement(ReplicaContext target, ReplicaContext source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.ReplicaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.ReplicaId);

        var requirements = new Dictionary<string, OriginSyncRequirement>();
        var targetDvv = target.GlobalVersionVector ?? new DottedVersionVector();
        var sourceDvv = source.GlobalVersionVector ?? new DottedVersionVector();

        var allSourceOrigins = new HashSet<string>(sourceDvv.Versions.Keys);
        allSourceOrigins.UnionWith(sourceDvv.Dots.Keys);

        foreach (var origin in allSourceOrigins)
        {
            sourceDvv.Versions.TryGetValue(origin, out var sourceMax);
            targetDvv.Versions.TryGetValue(origin, out var targetMax);

            targetDvv.Dots.TryGetValue(origin, out var targetDots);
            sourceDvv.Dots.TryGetValue(origin, out var sourceDots);

            var targetKnownDots = new HashSet<long>();
            var sourceMissingDots = new HashSet<long>();

            if (sourceMax > targetMax && targetDots != null)
            {
                foreach (var dot in targetDots)
                {
                    if (dot > targetMax && dot <= sourceMax)
                    {
                        targetKnownDots.Add(dot);
                    }
                }
            }

            if (sourceDots != null)
            {
                foreach (var dot in sourceDots)
                {
                    if (!targetDvv.Includes(origin, dot))
                    {
                        sourceMissingDots.Add(dot);
                    }
                }
            }

            if (sourceMax > targetMax || sourceMissingDots.Count > 0)
            {
                requirements[origin] = new OriginSyncRequirement
                {
                    TargetContiguousVersion = targetMax,
                    SourceContiguousVersion = sourceMax,
                    TargetKnownDots = targetKnownDots,
                    SourceMissingDots = sourceMissingDots
                };
            }
        }

        return new ReplicaSyncRequirement
        {
            TargetReplicaId = target.ReplicaId,
            SourceReplicaId = source.ReplicaId,
            RequirementsByOrigin = requirements
        };
    }

    /// <inheritdoc/>
    public BidirectionalSyncRequirements CalculateBidirectionalRequirements(ReplicaContext replicaA, ReplicaContext replicaB)
    {
        ArgumentNullException.ThrowIfNull(replicaA);
        ArgumentNullException.ThrowIfNull(replicaB);

        return new BidirectionalSyncRequirements
        {
            ReplicaANeedsFromB = CalculateRequirement(replicaA, replicaB),
            ReplicaBNeedsFromA = CalculateRequirement(replicaB, replicaA)
        };
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, long> CalculateGlobalMinimumVersionVector(IEnumerable<DottedVersionVector> clusterVectors)
    {
        ArgumentNullException.ThrowIfNull(clusterVectors);

        var vectors = clusterVectors.ToList();
        var gmvv = new Dictionary<string, long>();

        if (vectors.Count == 0)
        {
            return gmvv;
        }

        // Find all unique origin replicas across all vectors
        var allOrigins = vectors.SelectMany(v => v.Versions.Keys).Distinct().ToList();

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

        return gmvv;
    }

    /// <inheritdoc/>
    public DottedVersionVector CalculateGlobalMaximumVersionVector(IEnumerable<DottedVersionVector> clusterVectors)
    {
        ArgumentNullException.ThrowIfNull(clusterVectors);

        var mergedVersions = new Dictionary<string, long>();
        var mergedDots = new Dictionary<string, ISet<long>>();

        foreach (var state in clusterVectors)
        {
            if (state == null) continue;

            foreach (var kvp in state.Versions)
            {
                if (!mergedVersions.TryGetValue(kvp.Key, out var val) || kvp.Value > val)
                {
                    mergedVersions[kvp.Key] = kvp.Value;
                }
            }

            if (state.Dots != null)
            {
                foreach (var kvp in state.Dots)
                {
                    if (!mergedDots.TryGetValue(kvp.Key, out var dotSet))
                    {
                        dotSet = new HashSet<long>();
                        mergedDots[kvp.Key] = dotSet;
                    }
                    foreach (var dot in kvp.Value)
                    {
                        dotSet.Add(dot);
                    }
                }
            }
        }

        // Prune any dots that are safely covered by the max contiguous version
        var originsToPrune = new List<string>();
        foreach (var kvp in mergedDots)
        {
            if (mergedVersions.TryGetValue(kvp.Key, out var maxContiguous))
            {
                var prunedSet = new HashSet<long>(kvp.Value.Where(d => d > maxContiguous));
                if (prunedSet.Count > 0)
                {
                    mergedDots[kvp.Key] = prunedSet;
                }
                else
                {
                    originsToPrune.Add(kvp.Key);
                }
            }
        }

        foreach (var origin in originsToPrune)
        {
            mergedDots.Remove(origin);
        }

        return new DottedVersionVector(mergedVersions, mergedDots);
    }
}