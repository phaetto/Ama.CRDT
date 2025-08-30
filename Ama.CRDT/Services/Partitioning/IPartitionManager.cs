namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for managing a CRDT document that is partitioned across one or more streams,
/// allowing it to scale beyond available memory.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public interface IPartitionManager<T> where T : class
{
    /// <summary>
    /// Initializes a new partitioned CRDT document.
    /// </summary>
    /// <param name="dataStream">The stream to store the partitioned data.</param>
    /// <param name="indexStream">The stream to store the partition index.</param>
    /// <param name="initialObject">The initial object to populate the document with.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeAsync(Stream dataStream, Stream indexStream, T initialObject);

    /// <summary>
    /// Applies a CRDT patch to the partitioned document. The manager will locate the correct partition(s),
    /// load them, apply the changes, and handle any splits or merges that may result.
    /// </summary>
    /// <param name="patch">The CRDT patch to apply.</param>
    /// <returns>A task that represents the asynchronous patch application operation.</returns>
    Task ApplyPatchAsync(CrdtPatch patch);

    /// <summary>
    /// Retrieves the partition metadata for a given key.
    /// </summary>
    /// <param name="key">The key to find the partition for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the partition information, or null if not found.</returns>
    Task<Partition?> GetPartitionAsync(object key);

    /// <summary>
    /// Retrieves the deserialized content (data and metadata) of the partition that contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate the partition.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the data and metadata, or null if the partition is not found.
    /// </returns>
    Task<PartitionContent?> GetPartitionContentAsync(object key);
}