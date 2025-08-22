namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

public sealed class JsonCrdtService(IJsonCrdtPatcher patcher, IJsonCrdtApplicator applicator) : IJsonCrdtService
{
    public CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class
    {
        return patcher.GeneratePatch(original, modified);
    }

    public T Merge<T>(T document, CrdtPatch patch, CrdtMetadata metadata) where T : class
    {
        return applicator.ApplyPatch(document, patch, metadata);
    }
}