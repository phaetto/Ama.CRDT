namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly remove a node from a replicated tree by its identifier.
/// </summary>
/// <param name="NodeId">The unique identifier of the tree node to remove.</param>
public readonly record struct RemoveNodeIntent(object NodeId) : IOperationIntent;