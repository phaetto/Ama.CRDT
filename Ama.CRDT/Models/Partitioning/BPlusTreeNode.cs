namespace Ama.CRDT.Models.Partitioning;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a node in the B+ Tree index, containing keys and either child offsets (for internal nodes) or partition data (for leaf nodes).
/// </summary>
public sealed record BPlusTreeNode
{
    /// <summary>
    /// Gets or sets a value indicating whether this node is a leaf.
    /// </summary>
    public bool IsLeaf { get; set; }

    /// <summary>
    /// Gets or sets the list of keys in the node.
    /// </summary>
    public List<IComparable> Keys { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of offsets to child nodes. Only used in internal nodes.
    /// </summary>
    public List<long> ChildrenOffsets { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of partitions. Only used in leaf nodes.
    /// </summary>
    public List<IPartition> Partitions { get; set; } = new();
}