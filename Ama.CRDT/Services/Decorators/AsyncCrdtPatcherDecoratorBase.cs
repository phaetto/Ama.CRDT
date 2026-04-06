namespace Ama.CRDT.Services.Decorators;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;

/// <summary>
/// An abstract base class for <see cref="IAsyncCrdtPatcher"/> decorators.
/// Uses the <see cref="DecoratorBehavior"/> enum to strictly enforce generation flow.
/// </summary>
public abstract class AsyncCrdtPatcherDecoratorBase : IAsyncCrdtPatcher
{
    private readonly IAsyncCrdtPatcher innerPatcher;
    private readonly DecoratorBehavior behavior;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncCrdtPatcherDecoratorBase"/> class.
    /// </summary>
    /// <param name="innerPatcher">The inner patcher.</param>
    /// <param name="behavior">The execution phase this decorator instance will run in.</param>
    protected AsyncCrdtPatcherDecoratorBase(IAsyncCrdtPatcher innerPatcher, DecoratorBehavior behavior)
    {
        this.innerPatcher = innerPatcher ?? throw new ArgumentNullException(nameof(innerPatcher));
        this.behavior = behavior;
    }

    /// <inheritdoc/>
    public async Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, CancellationToken cancellationToken = default) where T : class
    {
        switch (this.behavior)
        {
            case DecoratorBehavior.Before:
                await OnBeforeGeneratePatchAsync(from, changed, cancellationToken).ConfigureAwait(false);
                return await this.innerPatcher.GeneratePatchAsync(from, changed, cancellationToken).ConfigureAwait(false);
            case DecoratorBehavior.After:
                var result = await this.innerPatcher.GeneratePatchAsync(from, changed, cancellationToken).ConfigureAwait(false);
                await OnAfterGeneratePatchAsync(from, changed, result, cancellationToken).ConfigureAwait(false);
                return result;
            case DecoratorBehavior.Complex:
                return await OnComplexGeneratePatchAsync(this.innerPatcher, from, changed, cancellationToken).ConfigureAwait(false);
            default:
                throw new NotSupportedException($"The decorator behavior '{this.behavior}' is not supported.");
        }
    }

    /// <inheritdoc/>
    public async Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, [DisallowNull] ICrdtTimestamp changeTimestamp, CancellationToken cancellationToken = default) where T : class
    {
        switch (this.behavior)
        {
            case DecoratorBehavior.Before:
                await OnBeforeGeneratePatchAsync(from, changed, changeTimestamp, cancellationToken).ConfigureAwait(false);
                return await this.innerPatcher.GeneratePatchAsync(from, changed, changeTimestamp, cancellationToken).ConfigureAwait(false);
            case DecoratorBehavior.After:
                var result = await this.innerPatcher.GeneratePatchAsync(from, changed, changeTimestamp, cancellationToken).ConfigureAwait(false);
                await OnAfterGeneratePatchAsync(from, changed, changeTimestamp, result, cancellationToken).ConfigureAwait(false);
                return result;
            case DecoratorBehavior.Complex:
                return await OnComplexGeneratePatchAsync(this.innerPatcher, from, changed, changeTimestamp, cancellationToken).ConfigureAwait(false);
            default:
                throw new NotSupportedException($"The decorator behavior '{this.behavior}' is not supported.");
        }
    }

    /// <inheritdoc/>
    public async Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken = default) where T : class
    {
        switch (this.behavior)
        {
            case DecoratorBehavior.Before:
                await OnBeforeGenerateOperationAsync(document, propertyExpression, intent, cancellationToken).ConfigureAwait(false);
                return await this.innerPatcher.GenerateOperationAsync(document, propertyExpression, intent, cancellationToken).ConfigureAwait(false);
            case DecoratorBehavior.After:
                var result = await this.innerPatcher.GenerateOperationAsync(document, propertyExpression, intent, cancellationToken).ConfigureAwait(false);
                await OnAfterGenerateOperationAsync(document, propertyExpression, intent, result, cancellationToken).ConfigureAwait(false);
                return result;
            case DecoratorBehavior.Complex:
                return await OnComplexGenerateOperationAsync(this.innerPatcher, document, propertyExpression, intent, cancellationToken).ConfigureAwait(false);
            default:
                throw new NotSupportedException($"The decorator behavior '{this.behavior}' is not supported.");
        }
    }

    /// <inheritdoc/>
    public async Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, [DisallowNull] ICrdtTimestamp timestamp, CancellationToken cancellationToken = default) where T : class
    {
        switch (this.behavior)
        {
            case DecoratorBehavior.Before:
                await OnBeforeGenerateOperationAsync(document, propertyExpression, intent, timestamp, cancellationToken).ConfigureAwait(false);
                return await this.innerPatcher.GenerateOperationAsync(document, propertyExpression, intent, timestamp, cancellationToken).ConfigureAwait(false);
            case DecoratorBehavior.After:
                var result = await this.innerPatcher.GenerateOperationAsync(document, propertyExpression, intent, timestamp, cancellationToken).ConfigureAwait(false);
                await OnAfterGenerateOperationAsync(document, propertyExpression, intent, timestamp, result, cancellationToken).ConfigureAwait(false);
                return result;
            case DecoratorBehavior.Complex:
                return await OnComplexGenerateOperationAsync(this.innerPatcher, document, propertyExpression, intent, timestamp, cancellationToken).ConfigureAwait(false);
            default:
                throw new NotSupportedException($"The decorator behavior '{this.behavior}' is not supported.");
        }
    }

    // --- Virtual Hooks ---

    protected virtual Task OnBeforeGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task OnAfterGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, CrdtPatch result, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task<CrdtPatch> OnComplexGeneratePatchAsync<T>(IAsyncCrdtPatcher inner, CrdtDocument<T> from, T changed, CancellationToken cancellationToken) where T : class => inner.GeneratePatchAsync(from, changed, cancellationToken);

    protected virtual Task OnBeforeGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task OnAfterGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp, CrdtPatch result, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task<CrdtPatch> OnComplexGeneratePatchAsync<T>(IAsyncCrdtPatcher inner, CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp, CancellationToken cancellationToken) where T : class => inner.GeneratePatchAsync(from, changed, changeTimestamp, cancellationToken);

    protected virtual Task OnBeforeGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CrdtOperation result, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task<CrdtOperation> OnComplexGenerateOperationAsync<T, TProp>(IAsyncCrdtPatcher inner, CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken) where T : class => inner.GenerateOperationAsync(document, propertyExpression, intent, cancellationToken);

    protected virtual Task OnBeforeGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CrdtOperation result, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
    protected virtual Task<CrdtOperation> OnComplexGenerateOperationAsync<T, TProp>(IAsyncCrdtPatcher inner, CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CancellationToken cancellationToken) where T : class => inner.GenerateOperationAsync(document, propertyExpression, intent, timestamp, cancellationToken);
}