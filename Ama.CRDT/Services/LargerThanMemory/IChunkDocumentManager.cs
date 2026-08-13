namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models;
using Ama.CRDT.Models.LargerThanMemory;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for querying and managing a CRDT document that is scaled beyond available memory. 
/// It provides a user-friendly API using property names (e.g., nameof(MyModel.MyProperty))
/// and specific methods for header chunks and virtual collection chunks.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public interface IChunkDocumentManager<T> where T : class, new()
{
    /// <summary>
    /// Initializes a new virtualized CRDT document.
    /// </summary>
    /// <param name="initialObject">The initial object to populate the document with. The logical key will be extracted from this object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeAsync(T initialObject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the header chunk for a given logical key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the chunk information, or null if not found.</returns>
    Task<IChunk?> GetHeaderChunkAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the deserialized content (data and metadata) of the header chunk for a given logical key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the header data and metadata as a <see cref="CrdtDocument{T}"/>, or null if the chunk is not found.
    /// </returns>
    Task<CrdtDocument<T>?> GetHeaderChunkContentAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a data chunk's metadata for a given key and property.
    /// </summary>
    /// <param name="key">The composite key (of type <see cref="CompositeChunkKey"/>) used to find the chunk.</param>
    /// <param name="propertyName">The name of the virtual property to search within (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the chunk information, or null if not found.</returns>
    Task<IChunk?> GetDataChunkAsync(CompositeChunkKey key, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the deserialized content (data and metadata) of the data chunk that contains the specified key for a given property.
    /// The content is merged with the header data to provide a complete view.
    /// </summary>
    /// <param name="key">The composite key (of type <see cref="CompositeChunkKey"/>) used to locate the chunk.</param>
    /// <param name="propertyName">The name of the virtual property to search within (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the data and metadata as a <see cref="CrdtDocument{T}"/>, or null if the chunk is not found.
    /// </returns>
    Task<CrdtDocument<T>?> GetDataChunkContentAsync(CompositeChunkKey key, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all data chunks for a given logical key and property as an asynchronously enumerable sequence.
    /// This method streams chunks and is suitable for large datasets.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document. Must implement <see cref="IComparable"/>.</param>
    /// <param name="propertyName">The name of the virtual property to retrieve chunks for (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An asynchronously enumerable sequence of data chunks, sorted by their start range key.</returns>
    IAsyncEnumerable<IChunk> GetAllDataChunksAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the count of all data chunks for a given logical key and property.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property to count chunks for (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of data chunks.</returns>
    Task<long> GetDataChunkCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single data chunk for a given logical key and property by its zero-based index.
    /// Chunks are ordered by their start range key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="index">The zero-based index of the data chunk to retrieve.</param>
    /// <param name="propertyName">The name of the virtual property to search within (e.g., `nameof(MyModel.Items)`).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the data chunk, or null if the index is out of bounds.</returns>
    Task<IChunk?> GetDataChunkByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all unique logical keys present across all managed indexes.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of unique logical keys.</returns>
    Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly triggers a garbage collection compaction pass across all logical keys and chunks.
    /// It utilizes the registered <see cref="GarbageCollection.ICompactionPolicyFactory"/> instances 
    /// to safely prune tombstones and compress metadata streams.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CompactAsync(CancellationToken cancellationToken = default);
}