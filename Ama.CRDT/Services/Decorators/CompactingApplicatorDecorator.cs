namespace Ama.CRDT.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services.GarbageCollection;

/// <summary>
/// A decorator for <see cref="IAsyncCrdtApplicator"/> that automatically runs garbage collection and compaction 
/// on the document's metadata immediately following the application of a patch.
/// This is highly effective for pure in-memory CRDT configurations without partitioning.
/// </summary>
public sealed class CompactingApplicatorDecorator : IAsyncCrdtApplicator
{
    private readonly IAsyncCrdtApplicator inner;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly IEnumerable<ICompactionPolicy> compactionPolicies;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactingApplicatorDecorator"/> class.
    /// </summary>
    /// <param name="inner">The inner applicator to delegate patch application to.</param>
    /// <param name="metadataManager">The service used to compact metadata.</param>
    /// <param name="compactionPolicies">The registered policies determining what is safe to compact.</param>
    public CompactingApplicatorDecorator(
        IAsyncCrdtApplicator inner,
        ICrdtMetadataManager metadataManager,
        IEnumerable<ICompactionPolicy> compactionPolicies)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
        this.compactionPolicies = compactionPolicies ?? throw new ArgumentNullException(nameof(compactionPolicies));
    }

    /// <inheritdoc/>
    public async Task<ApplyPatchResult<T>> ApplyPatchAsync<T>(CrdtDocument<T> document, CrdtPatch patch, CancellationToken cancellationToken = default) where T : class
    {
        var result = await inner.ApplyPatchAsync(document, patch, cancellationToken);

        if (compactionPolicies.Any())
        {
            foreach (var policy in compactionPolicies)
            {
                metadataManager.Compact<T>(document, policy);
            }
        }

        return result;
    }
}