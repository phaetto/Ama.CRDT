namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Pooling;
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
    ICrdtMetadataManager metadataManager,
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts,
    IShardIdProvider shardIdProvider,
    ICrdtStringPool? stringPool = null) : ICrdtApplicator
{
    private readonly ICrdtStringPool pool = stringPool ?? CrdtStringPool.Shared;

    /// <inheritdoc/>
    public ApplyPatchResult<T> ApplyPatch<T>(CrdtDocument<T> document, CrdtPatch patch) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(document.Metadata);

        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            return new ApplyPatchResult<T>(document, Array.Empty<UnappliedOperation>());
        }

        var fallbackShardId = pool.GetOrAdd(shardIdProvider.GetShardId(document));
        List<UnappliedOperation>? unappliedOperations = null;

        // Avoiding IEnumerator allocations by casting to IReadOnlyList when possible.
        if (patch.Operations is IReadOnlyList<CrdtOperation> operationsList)
        {
            int count = operationsList.Count;
            for (int i = 0; i < count; i++)
            {
                var status = ApplyOperation(document.Data, operationsList[i], document.Metadata, fallbackShardId);
                if (status != CrdtOperationStatus.Success)
                {
                    unappliedOperations ??= new List<UnappliedOperation>();
                    unappliedOperations.Add(new UnappliedOperation(operationsList[i], status));
                }
            }
        }
        else
        {
            foreach (var operation in patch.Operations)
            {
                var status = ApplyOperation(document.Data, operation, document.Metadata, fallbackShardId);
                if (status != CrdtOperationStatus.Success)
                {
                    unappliedOperations ??= new List<UnappliedOperation>();
                    unappliedOperations.Add(new UnappliedOperation(operation, status));
                }
            }
        }

        return new ApplyPatchResult<T>(document, unappliedOperations ?? (IReadOnlyList<UnappliedOperation>)Array.Empty<UnappliedOperation>());
    }

    private CrdtOperationStatus ApplyOperation(object document, CrdtOperation operation, CrdtMetadata metadata, string fallbackShardId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

        var strategy = strategyProvider.GetStrategy(operation, document);

        // Track causality globally on the replica context using the Shard DVVs.
        var shardId = !string.IsNullOrEmpty(operation.ShardId) ? pool.GetOrAdd(operation.ShardId) : fallbackShardId;

        if (operation.ShardClock > 0 && !string.IsNullOrEmpty(shardId))
        {
            if (!replicaContext.ShardVersionVectors.TryGetValue(shardId, out var shardVector))
            {
                shardVector = new DottedVersionVector();
                replicaContext.ShardVersionVectors[shardId] = shardVector;
            }
            shardVector.Add(pool.GetOrAdd(operation.ReplicaId), operation.ShardClock);
        }

        // Every operation now has a unique, monotonically increasing logical clock for its replica.
        // Therefore, if the operation's clock is less than OR EQUAL to the contiguous lastSeenClock,
        // it has already been applied and safely recorded.
        if (metadata.VersionVector.TryGetValue(operation.ReplicaId, out var lastSeenClock) &&
                operation.Clock <= lastSeenClock)
        {
            return CrdtOperationStatus.Obsolete;
        }

        if (!metadata.SeenExceptions.Add(operation))
        {
            return CrdtOperationStatus.Duplicate;
        }

        // The Applicator is responsible for resolving the path and instantiating missing intermediate POCOs.
        var resolution = PocoPathHelper.ResolvePath(document, operation.JsonPath, aotContexts, createMissing: true);
        
        if (resolution.Parent == null)
        {
            // Orphaned operation (e.g., parent map key or list item was concurrently deleted).
            // It is safely shadowed by the deletion, so we acknowledge it without applying to prevent divergence.
            metadataManager.AdvanceVersionVector(metadata, operation);
            return CrdtOperationStatus.Success;
        }

        var context = new ApplyOperationContext(document, metadata, operation)
        {
            Target = resolution.Parent,
            Property = resolution.Property,
            FinalSegment = resolution.FinalSegment
        };

        var strategyStatus = strategy.ApplyOperation(context);
        if (strategyStatus != CrdtOperationStatus.Success)
        {
            return strategyStatus;
        }

        metadataManager.AdvanceVersionVector(metadata, operation);
        return CrdtOperationStatus.Success;
    }
}