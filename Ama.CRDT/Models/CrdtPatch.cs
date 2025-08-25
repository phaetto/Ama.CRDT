namespace Ama.CRDT.Models;

/// <summary>
/// Represents a collection of CRDT operations that, when applied, transform one document state into another.
/// </summary>
/// <param name="Operations">A read-only list of the <see cref="CrdtOperation"/>s in this patch.</param>
public readonly record struct CrdtPatch(IReadOnlyList<CrdtOperation> Operations);