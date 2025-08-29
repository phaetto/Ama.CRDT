namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an operation that adds a node to a replicated tree.
/// </summary>
/// <param name="NodeId">The unique identifier of the new node.</param>
/// <param name="Value">The value of the new node.</param>
/// <param name="ParentId">The identifier of the parent node. Null for a root node.</param>
/// <param name="Tag">A unique tag for this specific addition, used for OR-Set logic.</param>
public readonly record struct TreeAddNodePayload(object NodeId, object? Value, object? ParentId, Guid Tag);