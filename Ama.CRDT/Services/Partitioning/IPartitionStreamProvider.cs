namespace Ama.CRDT.Services.Partitioning;

/// <summary>
/// Defines a contract for a service that provides data and index streams.
/// This allows for extensible storage strategies, such as using a single shared index file
/// and storing each logical partition's data in separate files or blob storage containers.
/// </summary>
public interface IPartitionStreamProvider
{
    /// <summary>
    /// Gets the single, shared index stream for the B+ Tree.
    /// Implementations should ensure that this method returns the same stream instance
    /// across the application's lifetime.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the index stream.</returns>
    Task<Stream> GetIndexStreamAsync();

    /// <summary>
    /// Gets the data stream for a specific logical partition key.
    /// </summary>
    /// <param name="logicalKey">The logical key identifying the data partition (e.g., a tenant ID).</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the data stream for the partition.</returns>
    Task<Stream> GetDataStreamAsync(object logicalKey);
}