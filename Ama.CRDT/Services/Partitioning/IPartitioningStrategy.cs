namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models.Partitioning;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for a strategy that manages the indexing of data partitions within a stream.
/// Implementations are responsible for creating, finding, and modifying the partition index.
/// </summary>
public interface IPartitioningStrategy
{
    /// <summary>
    /// Initializes the partitioning strategy for a given property, creating the necessary index structures if they don't exist.
    /// </summary>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializePropertyIndexAsync(string propertyName);

    /// <summary>
    /// Finds a partition that contains a specific key.
    /// </summary>
    /// <param name="key">The composite key to find within the partitions.</param>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the <see cref="IPartition"/> if found; otherwise, null.</returns>
    Task<IPartition?> FindPropertyPartitionAsync(CompositePartitionKey key, string propertyName);

    /// <summary>
    /// Inserts a new partition into the index. The key for indexing is determined by the partition type.
    /// </summary>
    /// <param name="partition">The partition to add.</param>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <returns>A task that represents the asynchronous insertion operation.</returns>
    Task InsertPropertyPartitionAsync(IPartition partition, string propertyName);

    /// <summary>
    /// Updates an existing partition in the index. The partition is identified by its key.
    /// </summary>
    /// <param name="partition">The partition with updated information.</param>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdatePropertyPartitionAsync(IPartition partition, string propertyName);

    /// <summary>
    /// Deletes a partition from the index, identified by its key.
    /// </summary>
    /// <param name="partition">The partition to delete.</param>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <returns>A task that represents the asynchronous deletion operation.</returns>
    Task DeletePropertyPartitionAsync(IPartition partition, string propertyName);

    /// <summary>
    /// Retrieves partitions from the index as an asynchronously enumerable sequence.
    /// This method streams partitions and is suitable for large datasets.
    /// </summary>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <param name="logicalKey">Optional. If provided, retrieves only partitions matching this logical key.</param>
    /// <returns>An asynchronously enumerable sequence of partitions.</returns>
    IAsyncEnumerable<IPartition> GetAllPropertyPartitionsAsync(string propertyName, IComparable? logicalKey = null);

    /// <summary>
    /// Gets the total number of partitions in the index.
    /// </summary>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <param name="logicalKey">Optional. If provided, returns the count of partitions for that logical key.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the total number of partitions.</returns>
    Task<long> GetPropertyPartitionCountAsync(string propertyName, IComparable? logicalKey = null);

    /// <summary>
    /// Retrieves a single data partition for a given logical key by its zero-based index.
    /// </summary>
    /// <param name="logicalKey">The logical key for which to retrieve the partition.</param>
    /// <param name="index">The zero-based index of the partition to retrieve.</param>
    /// <param name="propertyName">The name of the partitionable property this operation targets.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the data partition, or null if the index is out of bounds.</returns>
    Task<IPartition?> GetPropertyPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName);

    /// <summary>
    /// Initializes the partitioning strategy for the header, creating the necessary index structures if they don't exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeHeaderIndexAsync();

    /// <summary>
    /// Finds a header partition that contains a specific key.
    /// </summary>
    /// <param name="key">The composite key to find within the partitions.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the <see cref="IPartition"/> if found; otherwise, null.</returns>
    Task<IPartition?> FindHeaderPartitionAsync(CompositePartitionKey key);

    /// <summary>
    /// Inserts a new header partition into the index.
    /// </summary>
    /// <param name="partition">The partition to add.</param>
    /// <returns>A task that represents the asynchronous insertion operation.</returns>
    Task InsertHeaderPartitionAsync(IPartition partition);

    /// <summary>
    /// Updates an existing header partition in the index.
    /// </summary>
    /// <param name="partition">The partition with updated information.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateHeaderPartitionAsync(IPartition partition);

    /// <summary>
    /// Deletes a header partition from the index.
    /// </summary>
    /// <param name="partition">The partition to delete.</param>
    /// <returns>A task that represents the asynchronous deletion operation.</returns>
    Task DeleteHeaderPartitionAsync(IPartition partition);

    /// <summary>
    /// Retrieves header partitions from the index as an asynchronously enumerable sequence.
    /// </summary>
    /// <param name="logicalKey">Optional. If provided, retrieves only partitions matching this logical key.</param>
    /// <returns>An asynchronously enumerable sequence of partitions.</returns>
    IAsyncEnumerable<IPartition> GetAllHeaderPartitionsAsync(IComparable? logicalKey = null);

    /// <summary>
    /// Gets the total number of header partitions in the index.
    /// </summary>
    /// <param name="logicalKey">Optional. If provided, returns the count of partitions for that logical key.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the total number of partitions.</returns>
    Task<long> GetHeaderPartitionCountAsync(IComparable? logicalKey = null);
}