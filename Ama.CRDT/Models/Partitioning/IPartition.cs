namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// Defines the common properties for a partition in a partitioned data store.
/// </summary>
public interface IPartition
{
    /// <summary>
    /// Gets the byte offset in the data stream where this partition's data begins.
    /// </summary>
    long DataOffset { get; }

    /// <summary>
    /// Gets the length in bytes of this partition's data in the data stream.
    /// </summary>
    long DataLength { get; }

    /// <summary>
    /// Gets the byte offset in the data stream where this partition's metadata begins.
    /// </summary>
    long MetadataOffset { get; }

    /// <summary>
    /// Gets the length in bytes of this partition's metadata in the data stream.
    /// </summary>
    long MetadataLength { get; }

    /// <summary>
    /// Gets the key used to index this partition in the B+ Tree.
    /// </summary>
    /// <returns>The composite partition key for indexing.</returns>
    CompositePartitionKey GetPartitionKey();
}