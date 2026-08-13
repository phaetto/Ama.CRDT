namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.LargerThanMemory;

/// <summary>
/// Extends a virtual collection strategy with the ability to physically split
/// and merge disjoint segments (chunks) of data, typically for block/stream storage backends.
/// </summary>
public interface IChunkableCollectionStrategy : IVirtualCollectionStrategy
{
    /// <summary>
    /// Splits the data and metadata of a single, overfull chunk into two new disjoint chunks.
    /// </summary>
    /// <param name="originalData">The data object of the chunk being split.</param>
    /// <param name="originalMetadata">The metadata of the chunk being split.</param>
    /// <param name="virtualProperty">The property info of the virtual collection.</param>
    /// <returns>A <see cref="ChunkSplitResult"/> containing the content for the two new chunks and the range key at which the split occurred.</returns>
    ChunkSplitResult SplitToDisjoint(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo virtualProperty);

    /// <summary>
    /// Merges the data and metadata of two adjacent chunks into a single chunk.
    /// </summary>
    /// <param name="data1">The data object of the first chunk.</param>
    /// <param name="meta1">The metadata of the first chunk.</param>
    /// <param name="data2">The data object of the second chunk.</param>
    /// <param name="meta2">The metadata of the second chunk.</param>
    /// <param name="virtualProperty">The property info of the virtual collection.</param>
    /// <returns>A <see cref="ChunkContent"/> object containing the merged data and metadata.</returns>
    ChunkContent MergeDisjoint(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo virtualProperty);
}