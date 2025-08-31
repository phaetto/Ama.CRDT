namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// Represents the result of a partition split operation.
/// </summary>
/// <param name="Partition1">The content for the first new partition (covering the lower key range).</param>
/// <param name="Partition2">The content for the second new partition (covering the upper key range).</param>
/// <param name="SplitKey">The key that divides the two new partitions.</param>
public readonly record struct SplitResult(PartitionContent Partition1, PartitionContent Partition2, object SplitKey);