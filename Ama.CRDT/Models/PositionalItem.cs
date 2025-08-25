namespace Ama.CRDT.Models;

/// <summary>
/// A data structure used in CrdtOperation payloads for positional array updates.
/// It bundles the stable position with the actual value being inserted or updated.
/// </summary>
/// <param name="Position">The stable, fractional position string.</param>
/// <param name="Value">The actual object value of the list element.</param>
public readonly record struct PositionalItem(string Position, object? Value);