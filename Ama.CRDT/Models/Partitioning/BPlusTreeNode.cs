namespace Ama.CRDT.Models.Partitioning;

using System.Collections.Generic;

/// <summary>
/// Represents a node in the B+ Tree index, containing keys and either child offsets (for internal nodes) or partition data (for leaf nodes).
/// </summary>
public sealed class BPlusTreeNode
{
    /// <summary>
    /// Gets or sets a value indicating whether this node is a leaf.
    /// </summary>
    public bool IsLeaf { get; set; }

    /// <summary>
    /// Gets the list of keys in the node.
    /// </summary>
    public List<object> Keys { get; init; } = new();

    /// <summary>
    /// Gets the list of partitions (only in leaf nodes).
    /// </summary>
    public List<Partition> Partitions { get; init; } = new();

    /// <summary>
    /// Gets the list of child node offsets (only in internal nodes).
    /// </summary>
    public List<long> ChildrenOffsets { get; init; } = new();
}