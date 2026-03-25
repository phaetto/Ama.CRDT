namespace Ama.CRDT.Services.Decorators;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A decorator for <see cref="IIntentBuilder{TProperty}"/> that ensures operations 
/// generated via the fluent explicit intent API are recorded to an <see cref="ICrdtOperationJournal"/>.
/// </summary>
/// <typeparam name="TProperty">The type of the property being targeted.</typeparam>
internal sealed class JournalingIntentBuilderDecorator<TProperty> : IIntentBuilder<TProperty>
{
    private readonly IIntentBuilder<TProperty> innerBuilder;
    private readonly ICrdtOperationJournal journal;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalingIntentBuilderDecorator{TProperty}"/> class.
    /// </summary>
    /// <param name="innerBuilder">The inner builder to delegate the operation construction to.</param>
    /// <param name="journal">The journal service to record the generated operation.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerBuilder"/> or <paramref name="journal"/> is null.</exception>
    public JournalingIntentBuilderDecorator(IIntentBuilder<TProperty> innerBuilder, ICrdtOperationJournal journal)
    {
        ArgumentNullException.ThrowIfNull(innerBuilder);
        ArgumentNullException.ThrowIfNull(journal);

        this.innerBuilder = innerBuilder;
        this.journal = journal;
    }

    /// <inheritdoc/>
    public CrdtOperation Build(IOperationIntent intent)
    {
        var operation = this.innerBuilder.Build(intent);
        this.journal.Append(new[] { operation });
        return operation;
    }

    /// <inheritdoc/>
    public async Task<CrdtOperation> BuildAsync(IOperationIntent intent, CancellationToken cancellationToken = default)
    {
        var operation = await this.innerBuilder.BuildAsync(intent, cancellationToken).ConfigureAwait(false);
        await this.journal.AppendAsync(new[] { operation }, cancellationToken).ConfigureAwait(false);
        return operation;
    }
}