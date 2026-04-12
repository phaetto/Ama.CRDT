namespace Ama.CRDT.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtApplicator"/> that intercepts patch applications
/// and forwards successfully applied operations to an <see cref="ICrdtOperationJournal"/>.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public sealed class JournalingApplicatorDecorator : AsyncCrdtApplicatorDecoratorBase
{
    private readonly ICrdtOperationJournal journal;
    private readonly IDocumentIdProvider documentIdProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingApplicatorDecorator"/> class.
    /// </summary>
    /// <param name="innerApplicator">The inner applicator to delegate the actual patch application to.</param>
    /// <param name="journal">The journal service to record successfully applied operations.</param>
    /// <param name="documentIdProvider">The provider for extracting document IDs.</param>
    /// <param name="behavior">The explicitly chosen execution phase (enforced to be After).</param>
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
    protected override async Task OnAfterApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, ApplyPatchResult<TDoc> result, CancellationToken cancellationToken)
    {
        if (patch.Operations is { Count: > 0 })
        {
            var unappliedIds = new HashSet<Guid>(result.UnappliedOperations.Select(u => u.Operation.Id));
            var appliedOperations = patch.Operations.Where(op => !unappliedIds.Contains(op.Id)).ToList();

            if (appliedOperations.Count > 0)
            {
                var docId = this.documentIdProvider.GetDocumentId(document.Data);
                await this.journal.AppendAsync(docId, appliedOperations, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}