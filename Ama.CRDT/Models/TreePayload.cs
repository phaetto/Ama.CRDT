namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an operation that adds a node to a replicated tree.
/// </summary>
/// <param name="NodeId">The unique identifier of the new node.</param>
/// <param name="Value">The value of the new node.</param>
/// <param name="ParentId">The identifier of the parent node. Null for a root node.</param>
/// <param name="Tag">A unique tag for this specific addition, used for OR-Set logic.</param>
public readonly record struct TreeAddNodePayload(object NodeId, object? Value, object? ParentId, Guid Tag);

/// <summary>
/// Represents the payload for an operation that removes a node from a replicated tree.
/// </summary>
/// <param name="NodeId">The unique identifier of the node to remove.</param>
/// <param name="Tags">The set of tags associated with the node's additions that are being removed.</param>
public readonly record struct TreeRemoveNodePayload(object NodeId, ISet<Guid> Tags);

/// <summary>
/// Represents the payload for an operation that moves a node within a replicated tree.
/// </summary>
/// <param name="NodeId">The unique identifier of the node to move.</param>
/// <param name="NewParentId">The new parent's identifier. Null to move the node to the root.</param>
public readonly record struct TreeMoveNodePayload(object NodeId, object? NewParentId);