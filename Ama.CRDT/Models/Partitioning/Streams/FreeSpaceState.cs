namespace Ama.CRDT.Models.Partitioning.Streams;

using System.Collections.Generic;

/// <summary>
/// Encapsulates the state required for allocating and freeing space within a stream.
/// </summary>
public readonly record struct FreeSpaceState(long NextAvailableOffset, IReadOnlyList<FreeBlock>? FreeBlocks);