namespace Ama.CRDT.Services.Decorators;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;

/// <summary>
/// An abstract base class for <see cref="IAsyncCrdtMerger"/> decorators.
/// Uses the <see cref="DecoratorBehavior"/> enum to strictly enforce execution flow.
/// </summary>
public abstract class AsyncCrdtMergerDecoratorBase : IAsyncCrdtMerger
{
    private readonly IAsyncCrdtMerger innerMerger;
    private readonly DecoratorBehavior behavior;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncCrdtMergerDecoratorBase"/> class.
    /// </summary>
    /// <param name="innerMerger">The inner merger to delegate to.</param>
    /// <param name="behavior">The execution phase this decorator instance will run in.</param>
    protected AsyncCrdtMergerDecoratorBase(IAsyncCrdtMerger innerMerger, DecoratorBehavior behavior)
    {
        this.innerMerger = innerMerger ?? throw new ArgumentNullException(nameof(innerMerger));
        this.behavior = behavior;
    }

    /// <inheritdoc/>
    public async Task MergeStateAsync<T>([DisallowNull] CrdtDocument<T> primary, [DisallowNull] CrdtDocument<T> secondary, CancellationToken cancellationToken = default) where T : class
    {
        switch (this.behavior)
        {
            case DecoratorBehavior.Before:
                await OnBeforeMergeAsync(primary, secondary, cancellationToken).ConfigureAwait(false);
                await this.innerMerger.MergeStateAsync(primary, secondary, cancellationToken).ConfigureAwait(false);
                break;

            case DecoratorBehavior.After:
                await this.innerMerger.MergeStateAsync(primary, secondary, cancellationToken).ConfigureAwait(false);
                await OnAfterMergeAsync(primary, secondary, cancellationToken).ConfigureAwait(false);
                break;

            case DecoratorBehavior.Complex:
                await OnComplexMergeAsync(this.innerMerger, primary, secondary, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new NotSupportedException($"The decorator behavior '{this.behavior}' is not supported.");
        }
    }

    /// <summary>
    /// Invoked strictly before the inner merger is called. Only triggers if behavior is <see cref="DecoratorBehavior.Before"/>.
    /// </summary>
    protected virtual Task OnBeforeMergeAsync<T>(CrdtDocument<T> primary, CrdtDocument<T> secondary, CancellationToken cancellationToken) where T : class
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked strictly after the inner merger successfully executes. Only triggers if behavior is <see cref="DecoratorBehavior.After"/>.
    /// </summary>
    protected virtual Task OnAfterMergeAsync<T>(CrdtDocument<T> primary, CrdtDocument<T> secondary, CancellationToken cancellationToken) where T : class
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked when full pipeline control is required. Only triggers if behavior is <see cref="DecoratorBehavior.Complex"/>.
    /// The implementer is responsible for calling the inner merger.
    /// </summary>
    protected virtual Task OnComplexMergeAsync<T>(IAsyncCrdtMerger inner, CrdtDocument<T> primary, CrdtDocument<T> secondary, CancellationToken cancellationToken) where T : class
    {
        return inner.MergeStateAsync(primary, secondary, cancellationToken);
    }
}