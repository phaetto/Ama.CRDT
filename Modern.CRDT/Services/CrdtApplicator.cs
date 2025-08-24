namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;

public sealed class CrdtApplicator(ICrdtStrategyManager strategyManager) : ICrdtApplicator
{
    public T ApplyPatch<T>(T document, CrdtPatch patch, CrdtMetadata metadata) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(metadata);

        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            return document;
        }

        foreach (var operation in patch.Operations)
        {
            ApplyOperationWithStateCheck(document, operation, metadata);
        }

        return document;
    }

    private bool ApplyOperationWithStateCheck(object document, CrdtOperation operation, CrdtMetadata metadata)
    {
        // 1. Idempotency Check: Is the operation already seen?
        metadata.VersionVector.TryGetValue(operation.ReplicaId, out var vectorTs);
        if (vectorTs is not null && operation.Timestamp.CompareTo(vectorTs) <= 0)
        {
            return false; // Already covered by the version vector.
        }

        if (metadata.SeenExceptions.Contains(operation))
        {
            return false; // Seen as a previous out-of-order operation.
        }
        
        var strategy = strategyManager.GetStrategy(operation, document);

        // 2. Application Logic: Should the operation be applied based on its strategy?
        var applied = false;
        if (strategy is LwwStrategy)
        {
            metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs);
            if (lwwTs is null || operation.Timestamp.CompareTo(lwwTs) > 0)
            {
                strategy.ApplyOperation(document, operation);
                metadata.Lww[operation.JsonPath] = operation.Timestamp;
                applied = true;
            }
        }
        else // For Counter, ArrayLcs, etc., apply if it's a new operation.
        {
            strategy.ApplyOperation(document, operation);
            applied = true;
        }

        // 3. State Update: If applied, record it as a seen exception until the vector is advanced.
        if (applied)
        {
            metadata.SeenExceptions.Add(operation);
        }

        return applied;
    }
}