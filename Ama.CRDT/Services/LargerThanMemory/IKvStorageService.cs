namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a True Key-Value storage abstraction for CRDT data and metadata.
/// Maps individual collection items directly to individual storage rows without chunking,
/// removing the write-amplification bottleneck for externalized collections.
/// </summary>
public interface IKvStorageService
{
    /// <summary>
    /// Loads the deserialized content (data and metadata) of a document header.
    /// </summary>
    Task<CrdtDocument<TData>?> LoadHeaderAsync<TData>(IComparable logicalKey, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Saves the content (data and metadata) of a document header to the Key-Value storage.
    /// </summary>
    Task SaveHeaderAsync<TData>(IComparable logicalKey, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Loads the deserialized content (data and metadata) of a single virtual collection item.
    /// </summary>
    Task<CrdtDocument<TData>?> LoadItemAsync<TData>(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Saves the content (data and metadata) of a single virtual collection item to the Key-Value storage.
    /// </summary>
    Task SaveItemAsync<TData>(IComparable logicalKey, string propertyName, IComparable itemKey, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Deletes a specific item from a virtual collection within the Key-Value storage.
    /// </summary>
    Task DeleteItemAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all items for a given logical key and property as an asynchronously enumerable sequence.
    /// In a Key-Value backend, this should ideally utilize prefix or range queries.
    /// </summary>
    IAsyncEnumerable<KeyValuePair<IComparable, CrdtDocument<TData>>> GetItemsAsync<TData>(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) where TData : class;

    /// <summary>
    /// Gets the total number of items currently stored for a virtual property collection.
    /// </summary>
    Task<long> GetItemCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all stored item rows for a specific property collection under a logical key.
    /// </summary>
    Task ClearPropertyDataAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the stored header row for a logical key.
    /// </summary>
    Task ClearHeaderDataAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes any required indexes or structures on the storage engine.
    /// </summary>
    Task InitializeIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all unique logical document keys currently present in the storage.
    /// </summary>
    Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default);
}