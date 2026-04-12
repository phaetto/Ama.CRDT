namespace Ama.CRDT.Services.Versioning;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        return CalculateRequirement(
            target.ReplicaId, 
            target.GlobalVersionVector ?? new DottedVersionVector(), 
            source.ReplicaId, 
            source.GlobalVersionVector ?? new DottedVersionVector());
    }

    /// <inheritdoc/>
    public ReplicaSyncRequirement CalculateRequirement(string targetReplicaId, DottedVersionVector targetVector, string sourceReplicaId, DottedVersionVector sourceVector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetReplicaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReplicaId);
        ArgumentNullException.ThrowIfNull(targetVector);
        ArgumentNullException.ThrowIfNull(sourceVector);

        var requirements = new Dictionary<string, OriginSyncRequirement>();

        var allSourceOrigins = new HashSet<string>(sourceVector.Versions.Keys);
        allSourceOrigins.UnionWith(sourceVector.Dots.Keys);

        foreach (var origin in allSourceOrigins)
        {
            sourceVector.Versions.TryGetValue(origin, out var sourceMax);
            targetVector.Versions.TryGetValue(origin, out var targetMax);

            targetVector.Dots.TryGetValue(origin, out var targetDots);
            sourceVector.Dots.TryGetValue(origin, out var sourceDots);

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
                    if (!targetVector.Includes(origin, dot))
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
            TargetReplicaId = targetReplicaId,
            SourceReplicaId = sourceReplicaId,
            RequirementsByOrigin = requirements
        };
    }

    /// <inheritdoc/>
    public BidirectionalSyncRequirements CalculateBidirectionalRequirements(ReplicaContext replicaA, ReplicaContext replicaB)
    {
        ArgumentNullException.ThrowIfNull(replicaA);
        ArgumentNullException.ThrowIfNull(replicaB);

        return CalculateBidirectionalRequirements(
            replicaA.ReplicaId, 
            replicaA.GlobalVersionVector ?? new DottedVersionVector(), 
            replicaB.ReplicaId, 
            replicaB.GlobalVersionVector ?? new DottedVersionVector());
    }

    /// <inheritdoc/>
    public BidirectionalSyncRequirements CalculateBidirectionalRequirements(string replicaAId, DottedVersionVector vectorA, string replicaBId, DottedVersionVector vectorB)
    {
        return new BidirectionalSyncRequirements
        {
            ReplicaANeedsFromB = CalculateRequirement(replicaAId, vectorA, replicaBId, vectorB),
            ReplicaBNeedsFromA = CalculateRequirement(replicaBId, vectorB, replicaAId, vectorA)
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

    /// <inheritdoc/>
    public DottedVersionVector RemoveEvictedReplicas(DottedVersionVector vector, IEnumerable<string> evictedReplicaIds)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(evictedReplicaIds);

        var evictedSet = evictedReplicaIds as ISet<string> ?? new HashSet<string>(evictedReplicaIds);

        var newVersions = new Dictionary<string, long>();
        foreach (var kvp in vector.Versions)
        {
            if (!evictedSet.Contains(kvp.Key))
            {
                newVersions[kvp.Key] = kvp.Value;
            }
        }

        var newDots = new Dictionary<string, ISet<long>>();
        if (vector.Dots != null)
        {
            foreach (var kvp in vector.Dots)
            {
                if (!evictedSet.Contains(kvp.Key))
                {
                    newDots[kvp.Key] = new HashSet<long>(kvp.Value);
                }
            }
        }

        return new DottedVersionVector(newVersions, newDots);
    }

    /// <inheritdoc/>
    public async Task<JournalSyncResult> EvaluateJournalCompletionAsync(IAsyncEnumerable<JournaledOperation> retrievedOperations, ReplicaSyncRequirement requirement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retrievedOperations);
        ArgumentNullException.ThrowIfNull(requirement);

        if (!requirement.IsBehind)
        {
            return new JournalSyncResult(Array.Empty<JournaledOperation>(), false);
        }

        var allJournaledOps = new List<JournaledOperation>();
        await foreach (var jOp in retrievedOperations.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            allJournaledOps.Add(jOp);
        }

        bool journalTruncated = false;

        if (requirement.RequirementsByOrigin != null)
        {
            // Build a dictionary of hashed clocks to achieve O(1) checks per required dot
            var availableOpsLookup = new Dictionary<string, HashSet<long>>();
            foreach (var op in allJournaledOps)
            {
                if (!availableOpsLookup.TryGetValue(op.Operation.ReplicaId, out var clocks))
                {
                    clocks = new HashSet<long>();
                    availableOpsLookup[op.Operation.ReplicaId] = clocks;
                }
                clocks.Add(op.Operation.GlobalClock);
            }

            foreach (var kvp in requirement.RequirementsByOrigin)
            {
                var origin = kvp.Key;
                var req = kvp.Value;

                if (req.TargetContiguousVersion < req.SourceContiguousVersion)
                {
                    long firstRequiredClock = req.TargetContiguousVersion + 1;
                    
                    while (req.TargetKnownDots != null && req.TargetKnownDots.Contains(firstRequiredClock) && firstRequiredClock <= req.SourceContiguousVersion)
                    {
                        firstRequiredClock++;
                    }

                    if (firstRequiredClock <= req.SourceContiguousVersion)
                    {
                        bool hasRequired = availableOpsLookup.TryGetValue(origin, out var clocks) && clocks.Contains(firstRequiredClock);
                        if (!hasRequired)
                        {
                            journalTruncated = true;
                            break;
                        }
                    }
                }
                
                if (!journalTruncated && req.SourceMissingDots != null && req.SourceMissingDots.Count > 0)
                {
                    foreach (var dot in req.SourceMissingDots)
                    {
                        bool hasRequiredDot = availableOpsLookup.TryGetValue(origin, out var clocks) && clocks.Contains(dot);
                        if (!hasRequiredDot)
                        {
                            journalTruncated = true;
                            break;
                        }
                    }
                }

                if (journalTruncated) break;
            }
        }

        if (journalTruncated)
        {
            return new JournalSyncResult(Array.Empty<JournaledOperation>(), true);
        }

        return new JournalSyncResult(allJournaledOps, false);
    }
}