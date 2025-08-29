namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an operation that moves a node within a replicated tree.
/// </summary>
/// <param name="NodeId">The unique identifier of the node to move.</param>
/// <param name="NewParentId">The new parent's identifier. Null to move the node to the root.</param>
public readonly record struct TreeMoveNodePayload(object NodeId, object? NewParentId);