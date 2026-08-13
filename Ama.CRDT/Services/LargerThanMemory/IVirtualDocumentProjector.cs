namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Models.LargerThanMemory;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a projection hook for building CQRS Read Models from Virtualized CRDT documents.
/// This allows users to intercept and project converged data into relational databases or search indexes
/// immediately after patches are successfully applied, completely hiding CRDT complexities.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public interface IVirtualDocumentProjector<T> where T : class, new()
{
    /// <summary>
    /// Invoked when the header (root) document has been modified and successfully saved.
    /// </summary>
    Task ProjectHeaderAsync(IComparable logicalKey, T header, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked when a specific item in a virtual property collection is upserted (added or updated).
    /// Note: This item-level projection is natively supported by True Key-Value storage (IKvDocumentManager).
    /// </summary>
    Task ProjectItemUpsertAsync(IComparable logicalKey, string propertyName, IComparable itemKey, object item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked when a specific item in a virtual property collection is deleted.
    /// Note: This item-level projection is natively supported by True Key-Value storage (IKvDocumentManager).
    /// </summary>
    Task ProjectItemDeleteAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invoked when an entire chunk of a virtual property collection is modified.
    /// Note: This is specifically invoked by Chunked storage (IChunkDocumentManager).
    /// </summary>
    Task ProjectChunkAsync(IComparable logicalKey, string propertyName, IChunk chunk, T chunkData, CancellationToken cancellationToken = default);
}