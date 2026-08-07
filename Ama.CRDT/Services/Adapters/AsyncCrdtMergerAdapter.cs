namespace Ama.CRDT.Services.Adapters;

using Ama.CRDT.Models;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An adapter that bridges the synchronous <see cref="ICrdtMerger"/> to the asynchronous <see cref="IAsyncCrdtMerger"/> pipeline.
/// </summary>
internal sealed class AsyncCrdtMergerAdapter : IAsyncCrdtMerger
{
    private readonly ICrdtMerger innerMerger;

    public AsyncCrdtMergerAdapter(ICrdtMerger innerMerger)
    {
        ArgumentNullException.ThrowIfNull(innerMerger);
        this.innerMerger = innerMerger;
    }

    /// <inheritdoc/>
    public Task MergeStateAsync<T>([DisallowNull] CrdtDocument<T> primary, [DisallowNull] CrdtDocument<T> secondary, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Perform the pure in-memory state merge synchronously.
        this.innerMerger.MergeState(primary, secondary);
        
        return Task.CompletedTask;
    }
}