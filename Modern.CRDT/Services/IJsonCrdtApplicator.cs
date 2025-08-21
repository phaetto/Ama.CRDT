using Modern.CRDT.Models;

namespace Modern.CRDT.Services;

public interface IJsonCrdtApplicator
{
    CrdtDocument ApplyPatch(CrdtDocument document, CrdtPatch patch);
}