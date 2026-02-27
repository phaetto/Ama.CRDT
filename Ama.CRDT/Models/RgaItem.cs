namespace Ama.CRDT.Models;

/// <summary>
/// A data structure representing a single node/element in the RGA (Replicated Growable Array) sequence.
/// </summary>
/// <param name="Identifier">The unique identifier for this item.</param>
/// <param name="LeftIdentifier">The identifier of the item that immediately preceded this one at the time of insertion.</param>
/// <param name="Value">The actual value payload.</param>
/// <param name="IsDeleted">A tombstone flag indicating if this item has been removed.</param>
public readonly record struct RgaItem(
    RgaIdentifier Identifier, 
    RgaIdentifier? LeftIdentifier, 
    object? Value, 
    bool IsDeleted);