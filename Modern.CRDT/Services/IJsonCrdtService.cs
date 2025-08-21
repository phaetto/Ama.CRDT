namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

public interface IJsonCrdtService
{
    CrdtPatch CreatePatch(CrdtDocument original, CrdtDocument modified);

    CrdtDocument Merge(CrdtDocument original, CrdtPatch patch);

    CrdtDocument Merge(CrdtDocument original, CrdtDocument modified);

    CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;

    CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtPatch patch) where T : class;

    CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;
}