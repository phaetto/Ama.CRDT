namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly move a node in a replicated tree to a new parent.
/// </summary>
/// <param name="NodeId">The unique identifier of the tree node to move.</param>
/// <param name="NewParentId">The unique identifier of the new parent node, or null if it becomes a root.</param>
public readonly record struct MoveNodeIntent(object NodeId, object? NewParentId) : IOperationIntent;