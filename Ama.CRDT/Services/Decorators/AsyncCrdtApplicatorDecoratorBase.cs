namespace Ama.CRDT.Services.Decorators;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;

/// <summary>
/// An abstract base class for <see cref="IAsyncCrdtApplicator"/> decorators.
/// Uses the <see cref="DecoratorBehavior"/> enum to strictly enforce execution flow, ensuring developers 
/// cannot accidentally implement "Before" logic when the pipeline expects an "After" phase.
/// </summary>
public abstract class AsyncCrdtApplicatorDecoratorBase : IAsyncCrdtApplicator
{
    private readonly IAsyncCrdtApplicator innerApplicator;
    private readonly DecoratorBehavior behavior;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncCrdtApplicatorDecoratorBase"/> class.
    /// </summary>
    /// <param name="innerApplicator">The inner applicator to delegate to.</param>
    /// <param name="behavior">The execution phase this decorator instance will run in.</param>
    protected AsyncCrdtApplicatorDecoratorBase(IAsyncCrdtApplicator innerApplicator, DecoratorBehavior behavior)
    {
        this.innerApplicator = innerApplicator ?? throw new ArgumentNullException(nameof(innerApplicator));
        this.behavior = behavior;
    }

    /// <summary>
    /// Automatically orchestrates the execution flow based on the configured <see cref="DecoratorBehavior"/>.
    /// </summary>
    public async Task<ApplyPatchResult<TDoc>> ApplyPatchAsync<TDoc>([DisallowNull] CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken = default) where TDoc : class
    {
        switch (this.behavior)
        {
            case DecoratorBehavior.Before:
                await OnBeforeApplyAsync(document, patch, cancellationToken).ConfigureAwait(false);
                return await this.innerApplicator.ApplyPatchAsync(document, patch, cancellationToken).ConfigureAwait(false);

            case DecoratorBehavior.After:
                var result = await this.innerApplicator.ApplyPatchAsync(document, patch, cancellationToken).ConfigureAwait(false);
                await OnAfterApplyAsync(document, patch, result, cancellationToken).ConfigureAwait(false);
                return result;

            case DecoratorBehavior.Complex:
                return await OnComplexApplyAsync(this.innerApplicator, document, patch, cancellationToken).ConfigureAwait(false);

            default:
                throw new NotSupportedException($"The decorator behavior '{this.behavior}' is not supported.");
        }
    }

    /// <summary>
    /// Invoked strictly before the inner applicator is called. Only triggers if behavior is <see cref="DecoratorBehavior.Before"/>.
    /// </summary>
    protected virtual Task OnBeforeApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken) where TDoc : class
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked strictly after the inner applicator successfully executes. Only triggers if behavior is <see cref="DecoratorBehavior.After"/>.
    /// </summary>
    protected virtual Task OnAfterApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, ApplyPatchResult<TDoc> result, CancellationToken cancellationToken) where TDoc : class
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked when full pipeline control is required. Only triggers if behavior is <see cref="DecoratorBehavior.Complex"/>.
    /// The implementer is responsible for calling the inner applicator.
    /// </summary>
    protected virtual Task<ApplyPatchResult<TDoc>> OnComplexApplyAsync<TDoc>(IAsyncCrdtApplicator inner, CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken) where TDoc : class
    {
        return inner.ApplyPatchAsync(document, patch, cancellationToken);
    }
}