namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a high-level abstraction for saving and loading partitioned CRDT data and metadata.
/// This interface entirely hides Stream usage and pointer management, allowing the PartitionManager
/// to work purely with domain models.
/// </summary>
public interface IPartitionStorageService
{
    /// <summary>
    /// Loads the deserialized content (data and metadata) of a data partition.
    /// </summary>
    Task<CrdtDocument<TData>> LoadPartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partition, CancellationToken cancellationToken = default) where TData : class, new();

    /// <summary>
    /// Saves the content (data and metadata) of a data partition to storage and returns an updated partition object with the new storage offsets.
    /// </summary>
    Task<IPartition> SavePartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class, new();

    /// <summary>
    /// Loads the deserialized content (data and metadata) of a header partition.
    /// </summary>
    Task<CrdtDocument<TData>> LoadHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partition, CancellationToken cancellationToken = default) where TData : class, new();

    /// <summary>
    /// Saves the content (data and metadata) of a header partition to storage and returns an updated header partition object with the new storage offsets.
    /// </summary>
    Task<HeaderPartition> SaveHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class, new();

    /// <summary>
    /// Clears the stored data for a specific property partition.
    /// </summary>
    Task ClearPropertyDataAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the stored data for a specific header partition.
    /// </summary>
    Task ClearHeaderDataAsync(IComparable logicalKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a property partition index entry.
    /// </summary>
    Task SavePartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a header partition index entry.
    /// </summary>
    Task SaveHeaderPartitionAsync(IComparable logicalKey, HeaderPartition headerPartition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all data partitions for a given logical key and property as an asynchronously enumerable sequence.
    /// </summary>
    IAsyncEnumerable<IPartition> GetPartitionsAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the header partition for a given logical key.
    /// </summary>
    Task<HeaderPartition?> GetHeaderPartitionAsync(IComparable logicalKey, CancellationToken cancellationToken = default);
}