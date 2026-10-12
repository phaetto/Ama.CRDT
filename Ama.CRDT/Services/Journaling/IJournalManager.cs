namespace Ama.CRDT.Services.Journaling;

using System.Collections.Generic;
using System.Threading;
using Ama.CRDT.Models;

/// <summary>
/// Defines a service for retrieving missing CRDT operations from an operation journal 
/// based on synchronization requirements calculated between replicas for specific shards.
/// </summary>
public interface IJournalManager
{
    /// <summary>
    /// Retrieves a stream of operations that are missing in the target replica based on the provided requirements for a specific shard.
    /// </summary>
    /// <param name="shardId">The identifier of the shard to sync.</param>
    /// <param name="requirement">The synchronization requirement detailing missing contiguous versions and out-of-order dots.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An asynchronous stream of wrapped operations including their Document ID.</returns>
    IAsyncEnumerable<JournaledOperation> GetMissingOperationsAsync(string shardId, ReplicaSyncRequirement requirement, CancellationToken cancellationToken = default);
}