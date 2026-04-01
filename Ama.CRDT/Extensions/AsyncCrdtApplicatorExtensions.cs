namespace Ama.CRDT.Extensions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services;

/// <summary>
/// Provides extension methods for <see cref="IAsyncCrdtApplicator"/> to streamline common synchronization tasks.
/// </summary>
public static class AsyncCrdtApplicatorExtensions
{
    /// <summary>
    /// Asynchronously streams and applies a sequence of missing operations directly to the document.
    /// This handles the materialization of the stream into a patch and applies it seamlessly.
    /// </summary>
    /// <typeparam name="T">The type of the POCO model representing the document structure.</typeparam>
    /// <param name="applicator">The applicator instance.</param>
    /// <param name="document">The <see cref="CrdtDocument{T}"/> to apply the operations to.</param>
    /// <param name="missingOperations">An asynchronous stream of journaled operations.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task returning the <see cref="ApplyPatchResult{T}"/> containing the updated document.</returns>
    public static async Task<ApplyPatchResult<T>> ApplyOperationsAsync<T>(
        this IAsyncCrdtApplicator applicator,
        CrdtDocument<T> document,
        IAsyncEnumerable<JournaledOperation> missingOperations,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(applicator);
        ArgumentNullException.ThrowIfNull(missingOperations);

        var ops = new List<CrdtOperation>();
        await foreach (var jo in missingOperations.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ops.Add(jo.Operation);
        }

        if (ops.Count == 0)
        {
            return new ApplyPatchResult<T>(document, Array.Empty<UnappliedOperation>());
        }

        var patch = new CrdtPatch(ops);
        return await applicator.ApplyPatchAsync(document, patch, cancellationToken).ConfigureAwait(false);
    }
}