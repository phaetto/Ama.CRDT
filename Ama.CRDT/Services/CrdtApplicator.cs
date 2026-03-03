namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;

/// <summary>
/// Applies a CRDT patch to a document, handling conflict resolution and idempotency.
/// This implementation is thread-safe.
/// </summary>
public sealed class CrdtApplicator(
    ICrdtStrategyProvider strategyProvider,
    ICrdtMetadataManager metadataManager) : ICrdtApplicator
{
    /// <inheritdoc/>
    public T ApplyPatch<T>(CrdtDocument<T> document, CrdtPatch patch) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(document.Metadata);

        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            return document.Data;
        }

        // Avoiding IEnumerator allocations by casting to IReadOnlyList when possible.
        if (patch.Operations is IReadOnlyList<CrdtOperation> operationsList)
        {
            int count = operationsList.Count;
            for (int i = 0; i < count; i++)
            {
                ApplyOperation(document.Data, operationsList[i], document.Metadata);
            }
        }
        else
        {
            foreach (var operation in patch.Operations)
            {
                ApplyOperation(document.Data, operation, document.Metadata);
            }
        }

        return document.Data;
    }

    private void ApplyOperation(object document, CrdtOperation operation, CrdtMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

        var strategy = strategyProvider.GetStrategy(operation, document);

        // We use strictly less than (<) instead of (<=) because a single patch can contain multiple operations
        // with the same version/clock. Replayed duplicate operations for the current clock are caught by SeenExceptions.
        if (metadata.VersionVector.TryGetValue(operation.ReplicaId, out var lastSeenClock) &&
                operation.Clock < lastSeenClock)
        {
            return;
        }

        if (!metadata.SeenExceptions.Add(operation))
        {
            return;
        }

        // The Applicator is responsible for resolving the path and instantiating missing intermediate POCOs.
        var (target, property, finalSegment) = PocoPathHelper.ResolvePath(document, operation.JsonPath, createMissing: true);

        var context = new ApplyOperationContext(document, metadata, operation)
        {
            Target = target,
            Property = property,
            FinalSegment = finalSegment
        };

        strategy.ApplyOperation(context);

        metadataManager.AdvanceVersionVector(metadata, operation);
    }
}