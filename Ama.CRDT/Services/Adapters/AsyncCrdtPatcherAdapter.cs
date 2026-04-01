using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;

namespace Ama.CRDT.Services.Adapters;

/// <summary>
/// An adapter that bridges the synchronous <see cref="ICrdtPatcher"/> to the asynchronous <see cref="IAsyncCrdtPatcher"/> pipeline.
/// This acts as the final, bottom-level executor in the decorator chain, allowing the core in-memory CRDT math to remain synchronous 
/// while supporting async I/O decorators (like partitioning or journaling) above it.
/// </summary>
internal sealed class AsyncCrdtPatcherAdapter : IAsyncCrdtPatcher
{
    private readonly ICrdtPatcher _innerPatcher;

    public AsyncCrdtPatcherAdapter(ICrdtPatcher innerPatcher)
    {
        ArgumentNullException.ThrowIfNull(innerPatcher);
        _innerPatcher = innerPatcher;
    }

    /// <inheritdoc/>
    public Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = _innerPatcher.GeneratePatch(from, changed);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, [DisallowNull] ICrdtTimestamp changeTimestamp, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = _innerPatcher.GeneratePatch(from, changed, changeTimestamp);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = _innerPatcher.GenerateOperation(document, propertyExpression, intent);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, [DisallowNull] ICrdtTimestamp timestamp, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = _innerPatcher.GenerateOperation(document, propertyExpression, intent, timestamp);
        return Task.FromResult(result);
    }
}