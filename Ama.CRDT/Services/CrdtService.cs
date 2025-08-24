namespace Ama.CRDT.Services;

using Ama.CRDT.Models;

/// <inheritdoc/>
public sealed class CrdtService(ICrdtPatcher patcher, ICrdtApplicator applicator) : ICrdtService
{
    /// <inheritdoc/>
    public CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class
    {
        return patcher.GeneratePatch(original, modified);
    }

    /// <inheritdoc/>
    public T Merge<T>(T document, CrdtPatch patch, CrdtMetadata metadata) where T : class
    {
        return applicator.ApplyPatch(document, patch, metadata);
    }
}