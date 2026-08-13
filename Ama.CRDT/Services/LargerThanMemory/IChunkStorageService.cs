namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models;
using Ama.CRDT.Models.LargerThanMemory;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a high-level abstraction for saving and loading disjoint chunks of CRDT data and metadata.
/// This interface entirely hides Stream usage and index pointer management, allowing the ChunkedDocumentManager
/// to work purely with domain models.
/// </summary>
public interface IChunkStorageService
{
    /// <summary>
    /// Loads the deserialized content (data and metadata) of a data chunk.
    /// </summary>
    Task<CrdtDocument<TData>> LoadChunkContentAsync<TData>(IComparable logicalKey, string propertyName, IChunk chunk, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Saves the content (data and metadata) of a data chunk to storage and returns an updated chunk object with the new storage offsets.
    /// </summary>
    Task<IChunk> SaveChunkContentAsync<TData>(IComparable logicalKey, string propertyName, IChunk chunkToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Loads the deserialized content (data and metadata) of a header chunk.
    /// </summary>
    Task<CrdtDocument<TData>> LoadHeaderChunkContentAsync<TData>(IComparable logicalKey, HeaderChunk chunk, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Saves the content (data and metadata) of a header chunk to storage and returns an updated header chunk object with the new storage offsets.
    /// </summary>
    Task<HeaderChunk> SaveHeaderChunkContentAsync<TData>(IComparable logicalKey, HeaderChunk chunkToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Clears the stored data for a specific property collection.
    /// </summary>
    Task ClearPropertyDataAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the stored data for a specific header chunk.
    /// </summary>
    Task ClearHeaderDataAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the index for a given property.
    /// </summary>
    Task InitializePropertyIndexAsync(string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the header index.
    /// </summary>
    Task InitializeHeaderIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new property chunk into the index.
    /// </summary>
    Task InsertPropertyChunkAsync(string propertyName, IChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new header chunk into the index.
    /// </summary>
    Task InsertHeaderChunkAsync(IComparable logicalKey, HeaderChunk headerChunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing property chunk in the index.
    /// </summary>
    Task UpdatePropertyChunkAsync(string propertyName, IChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing header chunk in the index.
    /// </summary>
    Task UpdateHeaderChunkAsync(IComparable logicalKey, HeaderChunk headerChunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a property chunk from the index.
    /// </summary>
    Task DeletePropertyChunkAsync(string propertyName, IChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all data chunks for a given logical key and property as an asynchronously enumerable sequence.
    /// </summary>
    IAsyncEnumerable<IChunk> GetChunksAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a specific property chunk by its composite key.
    /// </summary>
    Task<IChunk?> GetPropertyChunkAsync(CompositeChunkKey key, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of property chunks for a logical key.
    /// </summary>
    Task<long> GetPropertyChunkCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a property chunk by its sequential index for a logical key.
    /// </summary>
    Task<IChunk?> GetPropertyChunkByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the header chunk for a given logical key.
    /// </summary>
    Task<HeaderChunk?> GetHeaderChunkAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all header chunks, optionally filtered by a logical key.
    /// </summary>
    IAsyncEnumerable<IChunk> GetAllHeaderChunksAsync(CancellationToken cancellationToken = default);
}