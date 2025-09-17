namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// Represents a header partition, which contains the parts of a document that are not part of the large, partitionable collection.
/// </summary>
/// <param name="Key">The composite key that identifies this header. The RangeKey component is typically null.</param>
/// <param name="DataOffset">The byte offset in the data stream where this partition's data begins.</param>
/// <param name="DataLength">The length in bytes of this partition's data in the data stream.</param>
/// <param name="MetadataOffset">The byte offset in the data stream where this partition's metadata begins.</param>
/// <param name="MetadataLength">The length in bytes of this partition's metadata in the data stream.</param>
public readonly record struct HeaderPartition(
    CompositePartitionKey Key,
    long DataOffset,
    long DataLength,
    long MetadataOffset,
    long MetadataLength
) : IPartition
{
    /// <inheritdoc/>
    public CompositePartitionKey GetPartitionKey() => Key;
}