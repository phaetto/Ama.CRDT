namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// Represents a partition in a partitioned data store, defining its key range and the location of its data and metadata in a stream.
/// </summary>
/// <param name="StartKey">The inclusive start key for the partition range.</param>
/// <param name="EndKey">The exclusive end key for the partition range. A null value represents infinity.</param>
/// <param name="DataOffset">The byte offset in the data stream where this partition's data begins.</param>
/// <param name="DataLength">The length in bytes of this partition's data in the data stream.</param>
/// <param name="MetadataOffset">The byte offset in the data stream where this partition's metadata begins.</param>
/// <param name="MetadataLength">The length in bytes of this partition's metadata in the data stream.</param>
public readonly record struct Partition(
    object StartKey,
    object? EndKey,
    long DataOffset,
    long DataLength,
    long MetadataOffset,
    long MetadataLength
);