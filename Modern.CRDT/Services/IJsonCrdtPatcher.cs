using Modern.CRDT.Models;

namespace Modern.CRDT.Services;

public interface IJsonCrdtPatcher
{
    CrdtPatch GeneratePatch(CrdtDocument from, CrdtDocument to);
}