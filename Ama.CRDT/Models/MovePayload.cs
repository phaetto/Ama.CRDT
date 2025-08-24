namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for a 'Move' operation in a list-based CRDT.
/// </summary>
/// <param name="ElementIdentifier">The stable identifier of the element being moved.</param>
/// <param name="TargetParentIdentifier">The stable identifier of the element after which the moved element should be placed. Null indicates moving to the beginning.</param>
public readonly record struct MovePayload(object ElementIdentifier, object? TargetParentIdentifier);