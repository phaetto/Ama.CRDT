namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for managing a CRDT document that is partitioned across one or more streams,
/// allowing it to scale beyond available memory.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public interface IPartitionManager<T> where T : class, new()
{
    /// <summary>
    /// Initializes a new partitioned CRDT document.
    /// </summary>
    /// <param name="initialObject">The initial object to populate the document with. The logical partition key will be extracted from this object.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeAsync(T initialObject);

    /// <summary>
    /// Applies a CRDT patch to the partitioned document. The manager will locate the correct partition(s),
    /// load them, apply the changes, and handle any splits or merges that may result.
    /// </summary>
    /// <param name="patch">The CRDT patch to apply.</param>
    /// <returns>A task that represents the asynchronous patch application operation.</returns>
    Task ApplyPatchAsync(CrdtPatch patch);

    /// <summary>
    /// Retrieves the partition metadata for a given key.
    /// </summary>
    /// <param name="key">The composite key (of type <see cref="CompositePartitionKey"/>) used to find the partition.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the partition information, or null if not found.</returns>
    Task<IPartition?> GetPartitionAsync(object key);

    /// <summary>
    /// Retrieves the deserialized content (data and metadata) of the partition that contains the specified key.
    /// </summary>
    /// <param name="key">The composite key (of type <see cref="CompositePartitionKey"/>) used to locate the partition.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the data and metadata as a <see cref="CrdtDocument{T}"/>, or null if the partition is not found.
    /// </returns>
    Task<CrdtDocument<T>?> GetPartitionContentAsync(object key);

    /// <summary>
    /// Retrieves all data partitions for a given logical key, sorted by their start range key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document. Must implement <see cref="IComparable"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a sorted list of data partitions.</returns>
    Task<List<IPartition>> GetAllDataPartitionsAsync(IComparable logicalKey);

    /// <summary>
    /// Retrieves all unique logical keys present in the index.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of unique logical keys.</returns>
    Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync();
}