namespace Ama.CRDT.Models;

/// <summary>
/// Represents a node within a <see cref="CrdtTree"/>.
/// </summary>
public sealed class TreeNode
{
    /// <summary>
    /// Gets the unique identifier of the node.
    /// </summary>
    public required object Id { get; init; }

    /// <summary>
    /// Gets or sets the value of the node.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the parent node. A null value indicates a root node.
    /// </summary>
    public object? ParentId { get; set; }
}

/// <summary>
/// Represents a replicated tree data structure where nodes can be added, removed, and moved concurrently.
/// </summary>
public sealed class CrdtTree
{
    /// <summary>
    /// Gets or sets the dictionary of nodes in the tree, keyed by their unique identifier.
    /// </summary>
    public IDictionary<object, TreeNode> Nodes { get; set; } = new Dictionary<object, TreeNode>();
}