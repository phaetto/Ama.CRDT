namespace Ama.CRDT.Services.LargerThanMemory;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;

/// <summary>
/// Provides a unified, read-only abstraction for accessing Larger-Than-Memory CRDT documents,
/// completely hiding the underlying storage mechanism (e.g., Chunked vs. True Key-Value).
/// This interface should be used by background batch applications that need to access the whole dataset wihtour paging/querying.
/// </summary>
/// <typeparam name="T">The root document type.</typeparam>
public interface IVirtualDocumentCollectionReader<T> where T : class, new()
{
    /// <summary>
    /// Retrieves the document header containing all non-virtualized properties, packed in a CRDT document.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The root document without the virtualized collections populated, or null if not found.</returns>
    Task<CrdtDocument<T>?> GetDocumentHeaderAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all elements of a specific virtual collection for a given logical key.
    /// </summary>
    /// <typeparam name="TElement">The expected type of the elements (e.g., a POCO or a <see cref="KeyValuePair{TKey, TValue}"/>).</typeparam>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property (e.g., <c>nameof(MyDocument.Comments)</c>).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An asynchronously enumerable sequence of strongly-typed elements.</returns>
    IAsyncEnumerable<TElement> GetElementsAsync<TElement>(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the total count of elements stored for a given virtual property.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the document.</param>
    /// <param name="propertyName">The name of the virtual property.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of items in the virtual collection.</returns>
    Task<long> GetElementCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all unique logical document keys currently present in the storage index.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of all logical keys.</returns>
    Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default);
}