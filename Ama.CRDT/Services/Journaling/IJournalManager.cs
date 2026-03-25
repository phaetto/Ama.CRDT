namespace Ama.CRDT.Services.Journaling;

using System.Collections.Generic;
using System.Threading;
using Ama.CRDT.Models;

/// <summary>
/// Defines a contract for managing the operation journal, specifically to retrieve 
/// operations that fulfill synchronization requirements between replicas.
/// </summary>
public interface IJournalManager
{
    /// <summary>
    /// Retrieves a stream of operations from the journal that fulfill the specified synchronization requirement.
    /// </summary>
    /// <param name="requirement">The synchronization requirement detailing missing causality.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An asynchronous stream of CRDT operations needed by the target replica.</returns>
    IAsyncEnumerable<CrdtOperation> GetMissingOperationsAsync(ReplicaSyncRequirement requirement, CancellationToken cancellationToken = default);
}