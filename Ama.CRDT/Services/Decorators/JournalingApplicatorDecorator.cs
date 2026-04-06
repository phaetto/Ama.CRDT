namespace Ama.CRDT.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtApplicator"/> that intercepts patch applications
/// and forwards successfully applied operations to an <see cref="ICrdtOperationJournal"/>.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public sealed class JournalingApplicatorDecorator : AsyncCrdtApplicatorDecoratorBase
{
    private readonly ICrdtOperationJournal journal;
    private readonly IEnumerable<CrdtAotContext> aotContexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingApplicatorDecorator"/> class.
    /// </summary>
    /// <param name="innerApplicator">The inner applicator to delegate the actual patch application to.</param>
    /// <param name="journal">The journal service to record successfully applied operations.</param>
    /// <param name="aotContexts">The AOT contexts to use for reflection-free property access.</param>
    /// <param name="behavior">The explicitly chosen execution phase (enforced to be After).</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerApplicator"/>, <paramref name="journal"/> or <paramref name="aotContexts"/> is null.</exception>
    public JournalingApplicatorDecorator(
        IAsyncCrdtApplicator innerApplicator, 
        ICrdtOperationJournal journal, 
        IEnumerable<CrdtAotContext> aotContexts,
        DecoratorBehavior behavior) : base(innerApplicator, behavior)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(aotContexts);

        this.journal = journal;
        this.aotContexts = aotContexts;
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
                var docId = PocoPathHelper.GetDocumentId(document.Data, this.aotContexts);
                await this.journal.AppendAsync(docId, appliedOperations, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}