namespace Ama.CRDT.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtPatcher"/> that intercepts explicit operation generation
/// and patch generation, forwarding the created operations to an <see cref="ICrdtOperationJournal"/>.
/// </summary>
public sealed class JournalingPatcherDecorator : IAsyncCrdtPatcher
{
    private readonly IAsyncCrdtPatcher innerPatcher;
    private readonly ICrdtOperationJournal journal;
    private readonly IEnumerable<CrdtContext> aotContexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingPatcherDecorator"/> class.
    /// </summary>
    /// <param name="innerPatcher">The inner patcher to delegate the generation to.</param>
    /// <param name="journal">The journal service to record generated operations.</param>
    /// <param name="aotContexts">The AOT contexts to use for reflection-free property access.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerPatcher"/>, <paramref name="journal"/> or <paramref name="aotContexts"/> is null.</exception>
    public JournalingPatcherDecorator(IAsyncCrdtPatcher innerPatcher, ICrdtOperationJournal journal, IEnumerable<CrdtContext> aotContexts)
    {
        ArgumentNullException.ThrowIfNull(innerPatcher);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(aotContexts);

        this.innerPatcher = innerPatcher;
        this.journal = journal;
        this.aotContexts = aotContexts;
    }

    /// <inheritdoc/>
    public async Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, CancellationToken cancellationToken = default) where T : class
    {
        var patch = await this.innerPatcher.GeneratePatchAsync(from, changed, cancellationToken).ConfigureAwait(false);
        
        if (patch.Operations is { Count: > 0 })
        {
            var docId = PocoPathHelper.GetDocumentId(from.Data, this.aotContexts);
            await this.journal.AppendAsync(docId, patch.Operations, cancellationToken).ConfigureAwait(false);
        }
        
        return patch;
    }

    /// <inheritdoc/>
    public async Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, [DisallowNull] ICrdtTimestamp changeTimestamp, CancellationToken cancellationToken = default) where T : class
    {
        var patch = await this.innerPatcher.GeneratePatchAsync(from, changed, changeTimestamp, cancellationToken).ConfigureAwait(false);
        
        if (patch.Operations is { Count: > 0 })
        {
            var docId = PocoPathHelper.GetDocumentId(from.Data, this.aotContexts);
            await this.journal.AppendAsync(docId, patch.Operations, cancellationToken).ConfigureAwait(false);
        }
        
        return patch;
    }

    /// <inheritdoc/>
    public async Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken = default) where T : class
    {
        var operation = await this.innerPatcher.GenerateOperationAsync(document, propertyExpression, intent, cancellationToken).ConfigureAwait(false);
        var docId = PocoPathHelper.GetDocumentId(document.Data, this.aotContexts);
        await this.journal.AppendAsync(docId, new[] { operation }, cancellationToken).ConfigureAwait(false);
        return operation;
    }

    /// <inheritdoc/>
    public async Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, [DisallowNull] ICrdtTimestamp timestamp, CancellationToken cancellationToken = default) where T : class
    {
        var operation = await this.innerPatcher.GenerateOperationAsync(document, propertyExpression, intent, timestamp, cancellationToken).ConfigureAwait(false);
        var docId = PocoPathHelper.GetDocumentId(document.Data, this.aotContexts);
        await this.journal.AppendAsync(docId, new[] { operation }, cancellationToken).ConfigureAwait(false);
        return operation;
    }
}