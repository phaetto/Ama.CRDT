namespace Ama.CRDT.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.GarbageCollection;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtApplicator"/> that automatically runs garbage collection and compaction 
/// on the document's metadata immediately following the application of a patch.
/// This is highly effective for pure in-memory CRDT configurations without partitioning.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public sealed class CompactingApplicatorDecorator : AsyncCrdtApplicatorDecoratorBase
{
    private readonly ICrdtMetadataManager metadataManager;
    private readonly IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactingApplicatorDecorator"/> class.
    /// </summary>
    /// <param name="inner">The inner applicator to delegate patch application to.</param>
    /// <param name="metadataManager">The service used to compact metadata.</param>
    /// <param name="compactionPolicyFactories">The registered factories determining what is safe to compact.</param>
    /// <param name="behavior">The explicitly chosen execution phase (enforced to be After).</param>
    public CompactingApplicatorDecorator(
        IAsyncCrdtApplicator inner,
        ICrdtMetadataManager metadataManager,
        IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories,
        DecoratorBehavior behavior) : base(inner, behavior)
    {
        this.metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
        this.compactionPolicyFactories = compactionPolicyFactories ?? throw new ArgumentNullException(nameof(compactionPolicyFactories));
    }

    /// <inheritdoc/>
    protected override Task OnAfterApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, ApplyPatchResult<TDoc> result, CancellationToken cancellationToken)
    {
        if (this.compactionPolicyFactories.Any())
        {
            foreach (var factory in this.compactionPolicyFactories)
            {
                var policy = factory.CreatePolicy();
                this.metadataManager.Compact(document, policy);
            }
        }

        return Task.CompletedTask;
    }
}