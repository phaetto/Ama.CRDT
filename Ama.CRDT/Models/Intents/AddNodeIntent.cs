namespace Ama.CRDT.Models.Intents;

using Ama.CRDT.Models;

/// <summary>
/// Represents the intent to explicitly add a node to a replicated tree.
/// </summary>
/// <param name="Node">The tree node to add.</param>
public readonly record struct AddNodeIntent(TreeNode Node) : IOperationIntent;