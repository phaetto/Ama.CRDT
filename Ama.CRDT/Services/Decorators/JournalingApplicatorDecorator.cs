namespace Ama.CRDT.Services.Decorators;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtApplicator"/> that intercepts patch applications
/// and forwards all operations to an <see cref="ICrdtOperationJournal"/> before they are applied.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.Before)]
public sealed class JournalingApplicatorDecorator : AsyncCrdtApplicatorDecoratorBase
{
    private readonly ICrdtOperationJournal journal;
    private readonly IDocumentIdProvider documentIdProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingApplicatorDecorator"/> class.
    /// </summary>
    /// <param name="innerApplicator">The inner applicator to delegate the actual patch application to.</param>
    /// <param name="journal">The journal service to record operations before application.</param>
    /// <param name="documentIdProvider">The provider for extracting document IDs.</param>
    /// <param name="behavior">The explicitly chosen execution phase (enforced to be Before).</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerApplicator"/>, <paramref name="journal"/> or <paramref name="documentIdProvider"/> is null.</exception>
    public JournalingApplicatorDecorator(
        IAsyncCrdtApplicator innerApplicator, 
        ICrdtOperationJournal journal, 
        IDocumentIdProvider documentIdProvider,
        DecoratorBehavior behavior) : base(innerApplicator, behavior)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(documentIdProvider);

        this.journal = journal;
        this.documentIdProvider = documentIdProvider;
    }

    /// <inheritdoc/>
    protected override async Task OnBeforeApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken)
    {
        if (patch.Operations is { Count: > 0 })
        {
            var docId = this.documentIdProvider.GetDocumentId(document.Data);
            await this.journal.AppendAsync(docId, patch.Operations, cancellationToken).ConfigureAwait(false);
        }
    }
}