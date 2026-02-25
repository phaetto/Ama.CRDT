namespace Ama.CRDT.Models.Partitioning.Streams;

using System.Collections.Generic;

/// <summary>
/// Represents the header of the B+ Tree index file, storing metadata like the root node offset,
/// degree of the tree, total partition count, the next available offset for writing, and a list of free blocks.
/// </summary>
public readonly record struct BTreeHeader(
    long RootNodeOffset = -1,
    long NextAvailableOffset = 0,
    int Degree = 16,
    long PartitionCount = 0,
    List<FreeBlock>? FreeBlocks = null
);

/// <summary>
/// Represents a contiguous block of free space within the stream that can be reused.
/// </summary>
public readonly record struct FreeBlock(long Offset, long Size);