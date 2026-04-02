namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Extends the <see cref="ICrdtStrategy"/> interface with methods for managing data and metadata across partitions.
/// Strategies that can operate on partitionable, larger-than-memory data structures should implement this interface.
/// </summary>
public interface IPartitionableCrdtStrategy : ICrdtStrategy
{
    /// <summary>
    /// Gets the initial range key from a document instance. This is used to determine the key for the very first data partition.
    /// </summary>
    /// <param name="data">The document object containing the partitionable collection.</param>
    /// <param name="partitionableProperty">The property info of the partitionable collection.</param>
    /// <returns>The start range key, or null if the collection is empty. The key must implement <see cref="IComparable"/>.</returns>
    IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty);

    /// <summary>
    /// Extracts a range key from a CRDT operation.
    /// This key is used by the <see cref="IPartitionManager{T}"/> to identify which data partition the operation should be applied to.
    /// If the operation does not apply to the partitionable collection (e.g., it's for a header field), this should return null.
    /// The key must implement <see cref="IComparable"/>.
    /// </summary>
    /// <param name="operation">The CRDT operation.</param>
    /// <param name="partitionablePropertyPath">The JSON path to the partitionable property.</param>
    /// <returns>The range key for the data partition, or null for a header partition operation.</returns>
    IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath);

    /// <summary>
    /// Gets the absolute minimum possible key for the strategy.
    /// This is used to create the first data partition when the partitionable collection is initially empty.
    /// </summary>
    /// <param name="partitionableProperty">The property info of the partitionable collection.</param>
    /// <returns>The minimum possible key. The key must implement <see cref="IComparable"/>.</returns>
    IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty);

    /// <summary>
    /// Splits the data and metadata of a single, overfull partition into two new partitions.
    /// </summary>
    /// <param name="originalData">The data object of the partition being split.</param>
    /// <param name="originalMetadata">The metadata of the partition being split.</param>
    /// <param name="partitionableProperty">The property info of the partitionable collection.</param>
    /// <returns>A <see cref="SplitResult"/> containing the content for the two new partitions and the range key at which the split occurred.</returns>
    SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty);

    /// <summary>
    /// Merges the data and metadata of two adjacent partitions into a single partition.
    /// </summary>
    /// <param name="data1">The data object of the first partition.</param>
    /// <param name="meta1">The metadata of the first partition.</param>
    /// <param name="data2">The data object of the second partition.</param>
    /// <param name="meta2">The metadata of the second partition.</param>
    /// <param name="partitionableProperty">The property info of the partitionable collection.</param>
    /// <returns>A <see cref="PartitionContent"/> object containing the merged data and metadata.</returns>
    PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty);
}