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
/// A decorator for <see cref="IAsyncCrdtMerger"/> that automatically runs garbage collection and compaction 
/// on the primary document's metadata immediately following the state merge.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public sealed class CompactingMergerDecorator : AsyncCrdtMergerDecoratorBase
{
    private readonly ICrdtMetadataManager metadataManager;
    private readonly IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactingMergerDecorator"/> class.
    /// </summary>
    /// <param name="inner">The inner merger to delegate the state merge to.</param>
    /// <param name="metadataManager">The service used to compact metadata.</param>
    /// <param name="compactionPolicyFactories">The registered factories determining what is safe to compact.</param>
    /// <param name="behavior">The explicitly chosen execution phase (enforced to be After).</param>
    public CompactingMergerDecorator(
        IAsyncCrdtMerger inner,
        ICrdtMetadataManager metadataManager,
        IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories,
        DecoratorBehavior behavior) : base(inner, behavior)
    {
        this.metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
        this.compactionPolicyFactories = compactionPolicyFactories ?? throw new ArgumentNullException(nameof(compactionPolicyFactories));
    }

    /// <inheritdoc/>
    protected override Task OnAfterMergeAsync<T>(CrdtDocument<T> primary, CrdtDocument<T> secondary, CancellationToken cancellationToken)
    {
        if (this.compactionPolicyFactories.Any())
        {
            foreach (var factory in this.compactionPolicyFactories)
            {
                var policy = factory.CreatePolicy();
                this.metadataManager.Compact(primary, policy);
            }
        }

        return Task.CompletedTask;
    }
}