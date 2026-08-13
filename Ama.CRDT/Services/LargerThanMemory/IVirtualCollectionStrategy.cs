namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Defines a CRDT strategy that supports externalizing its elements
/// into a Key-Value store or segmented storage.
/// </summary>
public interface IVirtualCollectionStrategy : ICrdtStrategy
{
    /// <summary>
    /// Gets the initial range key from a document instance. This is used to determine the key for the very first item/segment.
    /// </summary>
    /// <param name="data">The document object containing the virtual collection.</param>
    /// <param name="virtualProperty">The property info of the virtual collection.</param>
    /// <returns>The start range key, or null if the collection is empty. The key must implement <see cref="IComparable"/>.</returns>
    IComparable? GetStartKey(object data, CrdtPropertyInfo virtualProperty);

    /// <summary>
    /// Extracts a range key from a CRDT operation.
    /// This key is used to identify which specific storage item or chunk the operation should be applied to.
    /// </summary>
    /// <param name="operation">The CRDT operation.</param>
    /// <param name="virtualPropertyPath">The JSON path to the virtual property.</param>
    /// <returns>The storage key for the item/chunk, or null for a header operation.</returns>
    IComparable? GetKeyFromOperation(CrdtOperation operation, string virtualPropertyPath);

    /// <summary>
    /// Gets the absolute minimum possible key for the strategy.
    /// This is used to create the baseline minimum bound when the collection is initially empty.
    /// </summary>
    /// <param name="virtualProperty">The property info of the virtual collection.</param>
    /// <returns>The minimum possible key. The key must implement <see cref="IComparable"/>.</returns>
    IComparable GetMinimumKey(CrdtPropertyInfo virtualProperty);
}