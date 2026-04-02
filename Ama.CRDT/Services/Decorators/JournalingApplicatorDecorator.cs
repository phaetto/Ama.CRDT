namespace Ama.CRDT.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtApplicator"/> that intercepts patch applications
/// and forwards successfully applied operations to an <see cref="ICrdtOperationJournal"/>.
/// </summary>
public sealed class JournalingApplicatorDecorator : IAsyncCrdtApplicator
{
    private readonly IAsyncCrdtApplicator innerApplicator;
    private readonly ICrdtOperationJournal journal;
    private readonly IEnumerable<CrdtContext> aotContexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingApplicatorDecorator"/> class.
    /// </summary>
    /// <param name="innerApplicator">The inner applicator to delegate the actual patch application to.</param>
    /// <param name="journal">The journal service to record successfully applied operations.</param>
    /// <param name="aotContexts">The AOT contexts to use for reflection-free property access.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerApplicator"/>, <paramref name="journal"/> or <paramref name="aotContexts"/> is null.</exception>
    public JournalingApplicatorDecorator(IAsyncCrdtApplicator innerApplicator, ICrdtOperationJournal journal, IEnumerable<CrdtContext> aotContexts)
    {
        ArgumentNullException.ThrowIfNull(innerApplicator);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(aotContexts);

        this.innerApplicator = innerApplicator;
        this.journal = journal;
        this.aotContexts = aotContexts;
    }

    /// <inheritdoc/>
    public async Task<ApplyPatchResult<TDoc>> ApplyPatchAsync<TDoc>([DisallowNull] CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken = default) where TDoc : class
    {

        var result = await this.innerApplicator.ApplyPatchAsync(document, patch, cancellationToken).ConfigureAwait(false);

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

        return result;
    }
}