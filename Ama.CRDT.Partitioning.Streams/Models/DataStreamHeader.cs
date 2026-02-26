namespace Ama.CRDT.Partitioning.Streams.Models;

using System.Collections.Generic;

/// <summary>
/// Represents the header of a data stream, storing allocation metadata for reusing space.
/// </summary>
public record DataStreamHeader(
    long NextAvailableOffset = 1024,
    IReadOnlyList<FreeBlock>? FreeBlocks = null);