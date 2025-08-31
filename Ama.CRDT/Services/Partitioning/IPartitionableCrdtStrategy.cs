namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
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
    /// Gets the start key from a document instance. This is used to determine the key for the very first partition.
    /// </summary>
    /// <param name="data">The document object.</param>
    /// <returns>The start key, or null if the document is empty.</returns>
    object? GetStartKey(object data);

    /// <summary>
    /// Extracts a partitioning key from a CRDT operation.
    /// This key is used by the <see cref="IPartitionManager{T}"/> to identify which partition the operation should be applied to.
    /// </summary>
    /// <param name="operation">The CRDT operation.</param>
    /// <returns>The partitioning key, or null if the operation is not key-based or does not apply to this strategy.</returns>
    object? GetKeyFromOperation(CrdtOperation operation);

    /// <summary>
    /// Splits the data and metadata of a single, overfull partition into two new partitions.
    /// </summary>
    /// <param name="originalData">The data object of the partition being split.</param>
    /// <param name="originalMetadata">The metadata of the partition being split.</param>
    /// <param name="documentType">The type of the document (e.g., typeof(MyModel)).</param>
    /// <returns>A <see cref="SplitResult"/> containing the content for the two new partitions and the key at which the split occurred.</returns>
    SplitResult Split(object originalData, CrdtMetadata originalMetadata, Type documentType);

    /// <summary>
    /// Merges the data and metadata of two adjacent partitions into a single partition.
    /// </summary>
    /// <param name="data1">The data object of the first partition.</param>
    /// <param name="meta1">The metadata of the first partition.</param>
    /// <param name="data2">The data object of the second partition.</param>
    /// <param name="meta2">The metadata of the second partition.</param>
    /// <param name="documentType">The type of the document (e.g., typeof(MyModel)).</param>
    /// <returns>A <see cref="PartitionContent"/> object containing the merged data and metadata.</returns>
    PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, Type documentType);
}