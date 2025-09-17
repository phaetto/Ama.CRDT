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
    /// Initializes the partitioning strategy, creating the necessary index structures if they don't exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Finds a partition that contains a specific key.
    /// </summary>
    /// <param name="key">The composite key to find within the partitions.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the <see cref="Partition"/> if found; otherwise, null.</returns>
    Task<Partition?> FindPartitionAsync(CompositePartitionKey key);

    /// <summary>
    /// Inserts a new partition into the index. The <see cref="Partition.StartKey"/> must be a <see cref="CompositePartitionKey"/>.
    /// </summary>
    /// <param name="partition">The partition to add.</param>
    /// <returns>A task that represents the asynchronous insertion operation.</returns>
    Task InsertPartitionAsync(Partition partition);

    /// <summary>
    /// Updates an existing partition in the index. The partition is identified by its <see cref="Partition.StartKey"/>.
    /// The <see cref="Partition.StartKey"/> of the provided partition must match an existing entry and must be a <see cref="CompositePartitionKey"/>.
    /// </summary>
    /// <param name="partition">The partition with updated information.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdatePartitionAsync(Partition partition);

    /// <summary>
    /// Deletes a partition from the index, identified by its <see cref="Partition.StartKey"/>.
    /// The <see cref="Partition.StartKey"/> must be a <see cref="CompositePartitionKey"/>.
    /// </summary>
    /// <param name="partition">The partition to delete.</param>
    /// <returns>A task that represents the asynchronous deletion operation.</returns>
    Task DeletePartitionAsync(Partition partition);

    /// <summary>
    /// Retrieves all partitions from the index, sorted by their start key.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of all partitions.</returns>
    Task<List<Partition>> GetAllPartitionsAsync();
}