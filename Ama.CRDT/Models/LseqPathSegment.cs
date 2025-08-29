namespace Ama.CRDT.Models;

/// <summary>
/// Represents a single segment in an LSEQ identifier's path.
/// </summary>
/// <param name="Position">The integer position of the segment.</param>
/// <param name="ReplicaId">The replica ID that generated this segment.</param>
public readonly record struct LseqPathSegment(int Position, string ReplicaId);