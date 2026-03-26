namespace Ama.CRDT.Services.GarbageCollection;

/// <summary>
/// Defines a policy that determines whether a CRDT tombstone or metadata entry is safe to delete.
/// </summary>
public interface ICompactionPolicy
{
    /// <summary>
    /// Determines whether a specific CRDT tombstone is eligible for compaction/deletion based on its provided metadata.
    /// </summary>
    /// <param name="candidate">The metadata payload of the item being evaluated.</param>
    /// <returns>True if it is mathematically safe to remove the item; otherwise, false.</returns>
    bool IsSafeToCompact(CompactionCandidate candidate);
}