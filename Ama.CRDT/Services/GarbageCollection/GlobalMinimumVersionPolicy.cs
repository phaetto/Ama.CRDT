namespace Ama.CRDT.Services.GarbageCollection;

using System;
using System.Collections.Generic;

/// <summary>
/// A mathematically safe compaction policy based on the Global Minimum Version Vector (GMVV).
/// It ensures that a causal operation is only compacted if every known replica in the cluster has acknowledged it.
/// </summary>
public sealed class GlobalMinimumVersionPolicy : ICompactionPolicy
{
    private readonly IReadOnlyDictionary<string, long> globalMinimumVersions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalMinimumVersionPolicy"/> class.
    /// </summary>
    /// <param name="globalMinimumVersions">A dictionary mapping each origin replica ID to the lowest contiguous version acknowledged by all replicas in the cluster.</param>
    public GlobalMinimumVersionPolicy(IReadOnlyDictionary<string, long> globalMinimumVersions)
    {
        this.globalMinimumVersions = globalMinimumVersions ?? throw new ArgumentNullException(nameof(globalMinimumVersions));
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(CompactionCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.ReplicaId) || candidate.Version == null)
        {
            return false;
        }

        if (this.globalMinimumVersions.TryGetValue(candidate.ReplicaId, out var minVersion))
        {
            return candidate.Version.Value <= minVersion;
        }

        return false;
    }
}