namespace Ama.CRDT.Models;

public readonly record struct CrdtPatch(IReadOnlyList<CrdtOperation> Operations);