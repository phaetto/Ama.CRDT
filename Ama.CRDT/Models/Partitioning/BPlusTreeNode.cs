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
    /// Gets or sets the list of keys in the node.
    /// </summary>
    public List<object> Keys { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of offsets to child nodes. Only used in internal nodes.
    /// </summary>
    public List<long> ChildrenOffsets { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of partitions. Only used in leaf nodes.
    /// Stored as a list of objects to support polymorphic serialization of <see cref="IPartition"/> implementations.
    /// </summary>
    public List<object> Partitions { get; set; } = new();
}