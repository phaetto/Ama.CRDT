namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for managing a CRDT document using a True Key-Value backend.
/// It treats virtual collection elements as individual rows in the storage, bypassing chunk splits and merges.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public interface IKvDocumentManager<T> : IVirtualDocumentManager<T> where T : class, new()
{
    /// <summary>
    /// Retrieves the deserialized content (data and metadata) of a single virtual property item.
    /// The item is automatically attached to the header document to provide a valid target for operation application.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property to search within.</param>
    /// <param name="itemKey">The unique strategy-assigned key for the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CrdtDocument<T>?> GetItemAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the modified state of a single virtual property item back to the store.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property.</param>
    /// <param name="itemKey">The unique strategy-assigned key for the item.</param>
    /// <param name="itemDocument">The document carrying the single item and its metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SaveItemAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CrdtDocument<T> itemDocument, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific item from a virtual collection within the Key-Value storage.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property.</param>
    /// <param name="itemKey">The unique strategy-assigned key for the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeleteItemAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all items for a given logical key and property as an asynchronously enumerable sequence.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    IAsyncEnumerable<KeyValuePair<IComparable, CrdtDocument<T>>> GetAllItemsAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the count of all items stored for a given virtual property.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<long> GetItemCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);
}