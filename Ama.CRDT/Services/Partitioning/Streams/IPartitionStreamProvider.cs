namespace Ama.CRDT.Services.Partitioning.Streams;

using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Defines a contract for a service that provides data and index streams for partitioned documents.
/// This allows for extensible storage strategies, such as using separate files or blob storage containers for headers and properties.
/// </summary>
public interface IPartitionStreamProvider
{
    /// <summary>
    /// Gets the index stream for a specific partitionable property.
    /// Implementations should ensure that this method returns a consistent stream instance for a given property name.
    /// </summary>
    /// <param name="propertyName">The name of the partitionable property (e.g., "Comments").</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the index stream.</returns>
    Task<Stream> GetPropertyIndexStreamAsync(string propertyName);

    /// <summary>
    /// Gets the data stream for a specific logical partition key and property.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the data partition (e.g., a document ID).</param>
    /// <param name="propertyName">The name of the partitionable property (e.g., "Comments").</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the data stream for the partition.</returns>
    Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName);

    /// <summary>
    /// Gets the index stream for the header partitions.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the header index stream.</returns>
    Task<Stream> GetHeaderIndexStreamAsync();

    /// <summary>
    /// Gets the data stream for a specific header partition.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the header partition (e.g., a document ID).</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the data stream for the header partition.</returns>
    Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey);
}