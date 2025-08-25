namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services.Strategies;

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

    private void ApplyOperationWithStateCheck(object document, CrdtOperation operation, CrdtMetadata metadata)
    {
        var strategy = strategyManager.GetStrategy(operation, document);
        
        // Application Logic: The applicator acts as the gatekeeper for applying operations
        // based on metadata, while the strategy handles the actual data manipulation.
        if (strategy is LwwStrategy)
        {
            metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs);
            if (lwwTs is null || operation.Timestamp.CompareTo(lwwTs) > 0)
            {
                strategy.ApplyOperation(document, metadata, operation);
                metadata.Lww[operation.JsonPath] = operation.Timestamp;
            }
        }
        else // For Counter, ArrayLcs, etc., apply if it's a new operation.
        {
            if (metadata.SeenExceptions.Contains(operation))
            {
                return; // Seen as a previous out-of-order operation.
            }

            strategy.ApplyOperation(document, metadata, operation);
            metadata.SeenExceptions.Add(operation);
        }
    }
}