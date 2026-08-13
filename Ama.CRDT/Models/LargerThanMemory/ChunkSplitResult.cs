namespace Ama.CRDT.Models.LargerThanMemory;
/// <summary>
/// Represents the result of a partition split operation.
/// </summary>
/// <param name="Partition1">The content for the first new partition (covering the lower key range).</param>
/// <param name="Partition2">The content for the second new partition (covering the upper key range).</param>
/// <param name="SplitKey">The key that divides the two new partitions. Must implement <see cref="IComparable"/>.</param>
public readonly record struct ChunkSplitResult(ChunkContent Partition1, ChunkContent Partition2, IComparable SplitKey);