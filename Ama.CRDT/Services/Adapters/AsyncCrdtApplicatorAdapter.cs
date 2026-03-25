namespace Ama.CRDT.Services.Adapters;

using Ama.CRDT.Models;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An adapter that bridges the synchronous <see cref="ICrdtApplicator"/> to the asynchronous <see cref="IAsyncCrdtApplicator"/> pipeline.
/// This acts as the final, bottom-level executor in the decorator chain, allowing the core in-memory CRDT math to remain synchronous 
/// while supporting async I/O decorators (like partitioning or journaling) above it.
/// </summary>
internal sealed class AsyncCrdtApplicatorAdapter : IAsyncCrdtApplicator
{
    private readonly ICrdtApplicator _innerApplicator;

    public AsyncCrdtApplicatorAdapter(ICrdtApplicator innerApplicator)
    {
        ArgumentNullException.ThrowIfNull(innerApplicator);
        _innerApplicator = innerApplicator;
    }

    /// <inheritdoc/>
    public Task<ApplyPatchResult<T>> ApplyPatchAsync<T>([DisallowNull] CrdtDocument<T> document, CrdtPatch patch, CancellationToken cancellationToken = default) where T : class
    {
        // Fail fast if cancellation was requested before we even start
        cancellationToken.ThrowIfCancellationRequested();

        // Since the underlying base applicator handles pure in-memory CRDT math, 
        // it executes synchronously. We wrap the result in a completed task to satisfy the async interface.
        var result = _innerApplicator.ApplyPatch(document, patch);
        
        return Task.FromResult(result);
    }
}