namespace Ama.CRDT.Models;

/// <summary>
/// Represents a node within a <see cref="CrdtTree"/>.
/// </summary>
public sealed record TreeNode
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