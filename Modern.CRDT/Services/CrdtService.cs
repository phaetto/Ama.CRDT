namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

public sealed class CrdtService(ICrdtPatcher patcher, ICrdtApplicator applicator) : ICrdtService
{
    private readonly ICrdtPatcher patcher = patcher;
    private readonly ICrdtApplicator applicator = applicator;

    public CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class
    {
        return patcher.GeneratePatch(original, modified);
    }

    public T Merge<T>(T document, CrdtPatch patch, CrdtMetadata metadata) where T : class
    {
        return applicator.ApplyPatch(document, patch, metadata);
    }
}