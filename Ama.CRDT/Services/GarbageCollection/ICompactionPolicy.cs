using Ama.CRDT.Models;

namespace Ama.CRDT.Services.GarbageCollection;

/// <summary>
/// Defines a policy that determines whether a CRDT tombstone or metadata entry is safe to delete.
/// </summary>
public interface ICompactionPolicy
{
    /// <summary>
    /// Determines whether a specific wall-clock timestamp is eligible for compaction/deletion.
    /// </summary>
    /// <param name="timestamp">The timestamp of the operation or tombstone.</param>
    /// <returns>True if it is mathematically safe to remove the item; otherwise, false.</returns>
    bool IsSafeToCompact(ICrdtTimestamp timestamp);

    /// <summary>
    /// Determines whether a specific causal operation is eligible for compaction/deletion based on its origin and sequence version.
    /// </summary>
    /// <param name="replicaId">The identifier of the replica that originated the operation.</param>
    /// <param name="version">The causal sequence number of the operation.</param>
    /// <returns>True if it is mathematically safe to remove the item; otherwise, false.</returns>
    bool IsSafeToCompact(string replicaId, long version);
}