namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// Represents the header of the B+ Tree index file, storing metadata like the root node offset,
/// degree of the tree, total partition count, and the next available offset for writing.
/// </summary>
public readonly record struct BTreeHeader(
    long RootNodeOffset = -1,
    long NextAvailableOffset = 0,
    int Degree = 16,
    long PartitionCount = 0
);