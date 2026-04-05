namespace Ama.CRDT.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtPatcher"/> that intercepts explicit operation generation
/// and patch generation, forwarding the created operations to an <see cref="ICrdtOperationJournal"/>.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public sealed class JournalingPatcherDecorator : AsyncCrdtPatcherDecoratorBase
{
    private readonly ICrdtOperationJournal journal;
    private readonly IEnumerable<CrdtAotContext> aotContexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingPatcherDecorator"/> class.
    /// </summary>
    /// <param name="innerPatcher">The inner patcher to delegate the generation to.</param>
    /// <param name="journal">The journal service to record generated operations.</param>
    /// <param name="aotContexts">The AOT contexts to use for reflection-free property access.</param>
    /// <param name="behavior">The explicitly chosen execution phase (enforced to be After).</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerPatcher"/>, <paramref name="journal"/> or <paramref name="aotContexts"/> is null.</exception>
    public JournalingPatcherDecorator(
        IAsyncCrdtPatcher innerPatcher, 
        ICrdtOperationJournal journal, 
        IEnumerable<CrdtAotContext> aotContexts,
        DecoratorBehavior behavior) : base(innerPatcher, behavior)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(aotContexts);

        this.journal = journal;
        this.aotContexts = aotContexts;
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, CrdtPatch result, CancellationToken cancellationToken)
    {
        if (result.Operations is { Count: > 0 })
        {
            var docId = PocoPathHelper.GetDocumentId(from.Data, this.aotContexts);
            await this.journal.AppendAsync(docId, result.Operations, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp, CrdtPatch result, CancellationToken cancellationToken)
    {
        if (result.Operations is { Count: > 0 })
        {
            var docId = PocoPathHelper.GetDocumentId(from.Data, this.aotContexts);
            await this.journal.AppendAsync(docId, result.Operations, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CrdtOperation result, CancellationToken cancellationToken)
    {
        var docId = PocoPathHelper.GetDocumentId(document.Data, this.aotContexts);
        await this.journal.AppendAsync(docId, new[] { result }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CrdtOperation result, CancellationToken cancellationToken)
    {
        var docId = PocoPathHelper.GetDocumentId(document.Data, this.aotContexts);
        await this.journal.AppendAsync(docId, new[] { result }, cancellationToken).ConfigureAwait(false);
    }
}