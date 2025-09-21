namespace Ama.CRDT.Models.Partitioning;

using System;
using System.Collections.Generic;

public sealed record BPlusTreeNode
{
    public bool IsLeaf { get; set; }
    public List<IComparable> Keys { get; set; } = new();
    
    // For leaf nodes
    public List<IPartition> Partitions { get; set; } = new();
    
    // For internal nodes
    public List<long> ChildrenOffsets { get; set; } = new();
}