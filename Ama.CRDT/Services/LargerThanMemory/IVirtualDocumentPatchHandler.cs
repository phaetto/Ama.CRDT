namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a contract for handling patch application for virtualized CRDT documents (e.g., Chunked or KV partitioned).
/// </summary>
/// <typeparam name="TDoc">The document type.</typeparam>
public interface IVirtualDocumentPatchHandler<TDoc> where TDoc : class
{
    /// <summary>
    /// Attempts to apply the patch to the virtualized document. 
    /// Returns null if the document type is not configured for this specific handler.
    /// </summary>
    Task<ApplyPatchResult<TDoc>?> TryApplyPatchAsync(IAsyncCrdtApplicator innerApplicator, CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken = default);
}