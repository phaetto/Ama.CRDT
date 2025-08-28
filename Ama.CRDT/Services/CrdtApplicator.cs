namespace Ama.CRDT.Services;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using System;

/// <summary>
/// Applies a CRDT patch to a document, handling conflict resolution and idempotency.
/// This implementation is thread-safe.
/// </summary>
public sealed class CrdtApplicator(
    ICrdtStrategyProvider strategyProvider,
    ICrdtTimestampProvider timestampProvider,
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

        foreach (var operation in patch.Operations)
        {
            ApplyOperation(document.Data, operation, document.Metadata);
        }

        return document.Data;
    }

    private void ApplyOperation(object document, CrdtOperation operation, CrdtMetadata metadata)
    {
        var strategy = strategyProvider.GetStrategy(operation, document);

        var isSupportingVersionVector = timestampProvider.IsContinuous &&
            Attribute.IsDefined(strategy.GetType(), typeof(IdempotentWithContinuousTimeAttribute));
        if (isSupportingVersionVector)
        {
            if (metadata.VersionVector.TryGetValue(operation.ReplicaId, out var lastSeenTimestamp) &&
                operation.Timestamp.CompareTo(lastSeenTimestamp) <= 0)
            {
                return;
            }

            if (!metadata.SeenExceptions.Add(operation))
            {
                return;
            }
        }

        strategy.ApplyOperation(document, metadata, operation);

        if (isSupportingVersionVector)
        {
            metadataManager.AdvanceVersionVector(metadata, operation);
        }
    }
}