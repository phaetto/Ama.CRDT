namespace Ama.CRDT.Services.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.LargerThanMemory;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A global decorator that acts as a "Complex" interceptor for patch applications. 
/// It delegates virtual document operations (Chunked or KV) to the registered <see cref="IVirtualDocumentPatchHandler{TDoc}"/>,
/// enabling infinite scaling of CRDT collections without tying the applicator to a specific storage backend.
/// </summary>
[AllowedDecoratorBehavior(DecoratorBehavior.Complex)]
public sealed class PartitioningApplicatorDecorator : AsyncCrdtApplicatorDecoratorBase
{
    private readonly IServiceProvider serviceProvider;

    public PartitioningApplicatorDecorator(
        IAsyncCrdtApplicator innerApplicator,
        IServiceProvider serviceProvider,
        DecoratorBehavior behavior) : base(innerApplicator, behavior)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc/>
    protected override async Task<ApplyPatchResult<TDoc>> OnComplexApplyAsync<TDoc>(IAsyncCrdtApplicator inner, CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document.Data);

        var handlers = this.serviceProvider.GetServices<IVirtualDocumentPatchHandler<TDoc>>();
        foreach (var handler in handlers)
        {
            var result = await handler.TryApplyPatchAsync(inner, document, patch, cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                return result.Value;
            }
        }

        // If no virtual handler could process it, simply pass through to the inner applicator.
        return await inner.ApplyPatchAsync(document, patch, cancellationToken).ConfigureAwait(false);
    }
}