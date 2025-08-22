namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class JsonCrdtService(IJsonCrdtPatcher patcher, IJsonCrdtApplicator applicator) : IJsonCrdtService
{
    private static readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };

    public CrdtDocument Merge(CrdtDocument original, CrdtPatch patch)
    {
        return applicator.ApplyPatch(original, patch);
    }

    public CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class
    {
        return patcher.GeneratePatch(original, modified);
    }

    public CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtPatch patch) where T : class
    {
        var originalDoc = ToCrdtDocument(original);
        var mergedDoc = Merge(originalDoc, patch);

        return ToCrdtDocument<T>(mergedDoc);
    }

    public CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class
    {
        var patch = CreatePatch(original, modified);
        return Merge(original, patch);
    }

    private static CrdtDocument ToCrdtDocument<T>(CrdtDocument<T> doc) where T : class
    {
        var dataNode = doc.Data is null ? null : JsonSerializer.SerializeToNode(doc.Data, serializerOptions);
        return new CrdtDocument(dataNode, doc.Metadata?.DeepClone());
    }

    private static CrdtDocument<T> ToCrdtDocument<T>(CrdtDocument doc) where T : class
    {
        var data = doc.Data is null ? null : JsonSerializer.Deserialize<T>(doc.Data, serializerOptions);
        return new CrdtDocument<T>(data, doc.Metadata?.DeepClone());
    }
}