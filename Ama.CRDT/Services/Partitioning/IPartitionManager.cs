namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for querying and managing a CRDT document that is partitioned across one or more streams,
/// allowing it to scale beyond available memory. It provides a user-friendly API using property names (e.g., nameof(MyModel.MyProperty))
/// and specific methods for header partitions.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public interface IPartitionManager<T> where T : class, new()
{
    /// <summary>
    /// Initializes a new partitioned CRDT document.
    /// </summary>
    /// <param name="initialObject">The initial object to populate the document with. The logical partition key will be extracted from this object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeAsync(T initialObject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the header partition for a given logical key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the partition information, or null if not found.</returns>
    Task<IPartition?> GetHeaderPartitionAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the deserialized content (data and metadata) of the header partition for a given logical key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the header data and metadata as a <see cref="CrdtDocument{T}"/>, or null if the partition is not found.
    /// </returns>
    Task<CrdtDocument<T>?> GetHeaderPartitionContentAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a data partition's metadata for a given key and property.
    /// </summary>
    /// <param name="key">The composite key (of type <see cref="CompositePartitionKey"/>) used to find the partition.</param>
    /// <param name="propertyName">The name of the partitionable property to search within (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the partition information, or null if not found.</returns>
    Task<IPartition?> GetDataPartitionAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the deserialized content (data and metadata) of the data partition that contains the specified key for a given property.
    /// The content is merged with the header data to provide a complete view.
    /// </summary>
    /// <param name="key">The composite key (of type <see cref="CompositePartitionKey"/>) used to locate the partition.</param>
    /// <param name="propertyName">The name of the partitionable property to search within (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the data and metadata as a <see cref="CrdtDocument{T}"/>, or null if the partition is not found.
    /// </returns>
    Task<CrdtDocument<T>?> GetDataPartitionContentAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete, reconstructed object for a given logical key. It loads the root object and
    /// then loads and merges all data from all partitioned properties.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the fully reconstructed object, or null if not found.</returns>
    Task<T?> GetFullObjectAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all data partitions for a given logical key and property as an asynchronously enumerable sequence.
    /// This method streams partitions and is suitable for large datasets.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document. Must implement <see cref="IComparable"/>.</param>
    /// <param name="propertyName">The name of the partitionable property to retrieve partitions for (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An asynchronously enumerable sequence of data partitions, sorted by their start range key.</returns>
    IAsyncEnumerable<IPartition> GetAllDataPartitionsAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the count of all data partitions for a given logical key and property.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the partitionable property to count partitions for (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of data partitions.</returns>
    Task<long> GetDataPartitionCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single data partition for a given logical key and property by its zero-based index.
    /// Partitions are ordered by their start range key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="index">The zero-based index of the data partition to retrieve.</param>
    /// <param name="propertyName">The name of the partitionable property to search within (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the data partition, or null if the index is out of bounds.</returns>
    Task<IPartition?> GetDataPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all unique logical keys present across all managed indexes.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of unique logical keys.</returns>
    Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly triggers a garbage collection compaction pass across all logical keys and partitions.
    /// It utilizes the registered <see cref="Ama.CRDT.Services.GarbageCollection.ICompactionPolicyFactory"/> instances 
    /// to safely prune tombstones and compress metadata streams.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CompactAsync(CancellationToken cancellationToken = default);
}