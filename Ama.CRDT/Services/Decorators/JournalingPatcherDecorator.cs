namespace Ama.CRDT.Services.Decorators;

using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtPatcher"/> that intercepts explicit operation generation
/// and patch generation, forwarding the created operations to an <see cref="ICrdtOperationJournal"/>.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public sealed class JournalingPatcherDecorator : AsyncCrdtPatcherDecoratorBase
{
    private readonly ICrdtOperationJournal journal;
    private readonly IDocumentIdProvider documentIdProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingPatcherDecorator"/> class.
    /// </summary>
    /// <param name="innerPatcher">The inner patcher to delegate the generation to.</param>
    /// <param name="journal">The journal service to record generated operations.</param>
    /// <param name="documentIdProvider">The provider for extracting document IDs.</param>
    /// <param name="behavior">The explicitly chosen execution phase (enforced to be After).</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerPatcher"/>, <paramref name="journal"/> or <paramref name="documentIdProvider"/> is null.</exception>
    public JournalingPatcherDecorator(
        IAsyncCrdtPatcher innerPatcher, 
        ICrdtOperationJournal journal, 
        IDocumentIdProvider documentIdProvider,
        DecoratorBehavior behavior) : base(innerPatcher, behavior)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(documentIdProvider);

        this.journal = journal;
        this.documentIdProvider = documentIdProvider;
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, CrdtPatch result, CancellationToken cancellationToken)
    {
        if (result.Operations is { Count: > 0 })
        {
            var docId = this.documentIdProvider.GetDocumentId(from.Data);
            await this.journal.AppendAsync(docId, result.Operations, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp, CrdtPatch result, CancellationToken cancellationToken)
    {
        if (result.Operations is { Count: > 0 })
        {
            var docId = this.documentIdProvider.GetDocumentId(from.Data);
            await this.journal.AppendAsync(docId, result.Operations, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CrdtOperation result, CancellationToken cancellationToken)
    {
        var docId = this.documentIdProvider.GetDocumentId(document.Data);
        await this.journal.AppendAsync(docId, new[] { result }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CrdtOperation result, CancellationToken cancellationToken)
    {
        var docId = this.documentIdProvider.GetDocumentId(document.Data);
        await this.journal.AppendAsync(docId, new[] { result }, cancellationToken).ConfigureAwait(false);
    }
}