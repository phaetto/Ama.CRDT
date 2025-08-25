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