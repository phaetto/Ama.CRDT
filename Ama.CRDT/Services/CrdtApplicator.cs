namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services.Strategies;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Applies a CRDT patch to a document, handling conflict resolution and idempotency.
/// This implementation is thread-safe.
/// </summary>
public sealed class CrdtApplicator(ICrdtStrategyManager strategyManager) : ICrdtApplicator
{
    /// <inheritdoc/>
    public T ApplyPatch<T>([DisallowNull] T document, CrdtPatch patch, [DisallowNull] CrdtMetadata metadata) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            return document;
        }

        foreach (var operation in patch.Operations)
        {
            ApplyOperation(document, operation, metadata);
        }

        return document;
    }

    private void ApplyOperation(object document, CrdtOperation operation, CrdtMetadata metadata)
    {
        var strategy = strategyManager.GetStrategy(operation, document);
        strategy.ApplyOperation(document, metadata, operation);
    }
}