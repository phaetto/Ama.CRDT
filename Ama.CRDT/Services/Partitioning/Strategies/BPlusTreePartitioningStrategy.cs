namespace Ama.CRDT.Services.Partitioning.Strategies;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Partitioning.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// An implementation of <see cref="IPartitioningStrategy"/> that uses a B+ Tree to index partitions based on their start key.
/// The tree is stored and managed within a provided index stream.
/// </summary>
public sealed class BPlusTreePartitioningStrategy : IPartitioningStrategy
{
    private readonly IIndexSerializationHelper serializationHelper;
    private Stream? indexStream;
    private BTreeHeader header;
    private const int HeaderSize = 1024; // Reserve 1KB for header to allow for future expansion.

    public BPlusTreePartitioningStrategy(IIndexSerializationHelper serializationHelper)
    {
        this.serializationHelper = serializationHelper ?? throw new ArgumentNullException(nameof(serializationHelper));
        this.header = new BTreeHeader();
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(Stream indexStream)
    {
        this.indexStream = indexStream;
        this.header = new BTreeHeader(NextAvailableOffset: HeaderSize); // Data starts after the header
        
        // Truncate stream if it's being re-initialized
        this.indexStream.SetLength(HeaderSize);

        var root = new BPlusTreeNode { IsLeaf = true };
        var rootOffset = await AllocateAndWriteNodeAsync(root);
        
        this.header = this.header with { RootNodeOffset = rootOffset };
        await serializationHelper.WriteHeaderAsync(this.indexStream, this.header, HeaderSize);
    }

    /// <inheritdoc/>
    public async Task<Partition?> FindPartitionAsync(object key)
    {
        if (indexStream is null)
        {
            throw new InvalidOperationException("Strategy has not been initialized.");
        }

        if (indexStream.Length <= HeaderSize)
        {
            return null; // No data besides a potentially empty header, so no partitions.
        }
        
        // Always re-read the header to ensure freshness, as the strategy is a singleton
        // and its stream can be re-initialized by different managers across different tests or contexts.
        var currentHeader = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        
        if (currentHeader.RootNodeOffset == -1)
        {
             // A valid header was written but it points to no root. Empty tree.
             return null;
        }
        
        return await FindInNodeAsync(currentHeader.RootNodeOffset, key);
    }

    /// <inheritdoc/>
    public async Task InsertPartitionAsync(Partition partition)
    {
        if (indexStream is null)
        {
            throw new InvalidOperationException("Strategy must be initialized before inserting.");
        }

        this.header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Strategy has not been initialized correctly, root node is missing.");
        }

        var root = await serializationHelper.ReadNodeAsync(indexStream, header.RootNodeOffset);
        if (root.Keys.Count == (2 * header.Degree - 1)) // Root is full
        {
            // Split the root
            var oldRootOffset = header.RootNodeOffset;
            var newRoot = new BPlusTreeNode();
            newRoot.ChildrenOffsets.Add(oldRootOffset);
            
            await SplitChildAsync(newRoot, 0, root, oldRootOffset);
            
            var newRootOffset = await AllocateAndWriteNodeAsync(newRoot);
            header = header with { RootNodeOffset = newRootOffset };
            
            // After splitting the root, the insertion must proceed from the new root.
            var finalRootOffset = await InsertNonFullAsync(newRoot, newRootOffset, partition);
            header = header with { RootNodeOffset = finalRootOffset };
        }
        else
        {
            var newRootOffset = await InsertNonFullAsync(root, header.RootNodeOffset, partition);
            header = header with { RootNodeOffset = newRootOffset };
        }
        await serializationHelper.WriteHeaderAsync(indexStream, header, HeaderSize);
    }

    /// <inheritdoc/>
    public async Task UpdatePartitionAsync(Partition partition)
    {
        if (indexStream is null)
        {
            throw new InvalidOperationException("Strategy has not been initialized.");
        }

        this.header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Strategy has not been initialized correctly, root node is missing.");
        }

        // This recursive method handles finding the partition, updating it,
        // and safely re-allocating nodes on the path back to the root
        // to reflect updated child pointers.
        var newRootOffset = await UpdateInNodeAsync(header.RootNodeOffset, partition);

        if (newRootOffset != header.RootNodeOffset)
        {
            header = header with { RootNodeOffset = newRootOffset };
            await serializationHelper.WriteHeaderAsync(indexStream, header, HeaderSize);
        }
    }

    /// <inheritdoc/>
    public async Task DeletePartitionAsync(Partition partition)
    {
        if (indexStream is null)
        {
            throw new InvalidOperationException("Strategy must be initialized before deleting.");
        }

        this.header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Cannot delete from an empty tree.");
        }

        var key = partition.StartKey;
        var newRootOffset = await DeleteRecursiveAsync(header.RootNodeOffset, key);

        var root = await serializationHelper.ReadNodeAsync(indexStream, newRootOffset);

        // If the root node is an internal node with no keys, it means its last two children were merged.
        // The merged child becomes the new root, decreasing the tree height.
        if (!root.IsLeaf && root.Keys.Count == 0 && root.ChildrenOffsets.Count == 1)
        {
            newRootOffset = root.ChildrenOffsets[0];
        }

        if (newRootOffset != header.RootNodeOffset)
        {
            header = header with { RootNodeOffset = newRootOffset };
            await serializationHelper.WriteHeaderAsync(indexStream, header, HeaderSize);
        }
    }
    
    /// <inheritdoc/>
    public async Task<List<Partition>> GetAllPartitionsAsync()
    {
        if (indexStream is null || indexStream.Length <= HeaderSize)
        {
            return new List<Partition>();
        }

        var currentHeader = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (currentHeader.RootNodeOffset == -1)
        {
            return new List<Partition>();
        }

        var allPartitions = new List<Partition>();
        await GetAllPartitionsRecursiveAsync(currentHeader.RootNodeOffset, allPartitions);

        allPartitions.Sort((p1, p2) => Comparer<object>.Default.Compare(p1.StartKey, p2.StartKey));

        return allPartitions;
    }

    private async Task GetAllPartitionsRecursiveAsync(long nodeOffset, List<Partition> partitions)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream!, nodeOffset);
        if (node.IsLeaf)
        {
            partitions.AddRange(node.Partitions);
        }
        else
        {
            foreach (var childOffset in node.ChildrenOffsets)
            {
                await GetAllPartitionsRecursiveAsync(childOffset, partitions);
            }
        }
    }

    private async Task<long> DeleteRecursiveAsync(long nodeOffset, object key)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream!, nodeOffset);
        int t = header.Degree;
        bool nodeModified = false;

        if (node.IsLeaf)
        {
            int keyIndex = node.Keys.FindIndex(k => Comparer<object>.Default.Compare(k, key) == 0);
            if (keyIndex == -1)
            {
                throw new KeyNotFoundException($"Could not find a partition with start key '{key}' to delete.");
            }

            node.Keys.RemoveAt(keyIndex);
            node.Partitions.RemoveAt(keyIndex);
            return await AllocateAndWriteNodeAsync(node);
        }

        // It's an internal node.
        int childIndex = 0;
        while (childIndex < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }

        var childOffset = node.ChildrenOffsets[childIndex];
        var childNode = await serializationHelper.ReadNodeAsync(indexStream!, childOffset);
        
        if (childNode.Keys.Count < t)
        {
            // Rebalance before descending if there are siblings to interact with.
            if (childIndex > 0) // Has left sibling
            {
                var leftSiblingOffset = node.ChildrenOffsets[childIndex - 1];
                var leftSibling = await serializationHelper.ReadNodeAsync(indexStream!, leftSiblingOffset);
                if (leftSibling.Keys.Count >= t) // Borrow from left
                {
                    await BorrowFromSiblingAsync(node, childIndex, childNode, leftSibling, isLeftSibling: true);
                    node.ChildrenOffsets[childIndex - 1] = await AllocateAndWriteNodeAsync(leftSibling);
                    childOffset = await AllocateAndWriteNodeAsync(childNode);
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else // Merge with left
                {
                    var mergedNode = await MergeWithSiblingAsync(node, childIndex, childNode, leftSibling, isLeftSibling: true);
                    childIndex--;
                    childOffset = await AllocateAndWriteNodeAsync(mergedNode);
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
            else if (childIndex + 1 < node.ChildrenOffsets.Count) // Has right sibling
            {
                var rightSiblingOffset = node.ChildrenOffsets[childIndex + 1];
                var rightSibling = await serializationHelper.ReadNodeAsync(indexStream!, rightSiblingOffset);
                if (rightSibling.Keys.Count >= t) // Borrow from right
                {
                    await BorrowFromSiblingAsync(node, childIndex, childNode, rightSibling, isLeftSibling: false);
                    node.ChildrenOffsets[childIndex + 1] = await AllocateAndWriteNodeAsync(rightSibling);
                    childOffset = await AllocateAndWriteNodeAsync(childNode);
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else // Merge with right
                {
                    var mergedNode = await MergeWithSiblingAsync(node, childIndex, childNode, rightSibling, isLeftSibling: false);
                    childOffset = await AllocateAndWriteNodeAsync(mergedNode);
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
        }

        var newChildOffset = await DeleteRecursiveAsync(childOffset, key);
        if (newChildOffset != childOffset)
        {
            node.ChildrenOffsets[childIndex] = newChildOffset;
            nodeModified = true;
        }

        if (nodeModified)
        {
            return await AllocateAndWriteNodeAsync(node);
        }

        return nodeOffset;
    }
    
    private Task BorrowFromSiblingAsync(BPlusTreeNode parent, int childIndex, BPlusTreeNode child, BPlusTreeNode sibling, bool isLeftSibling)
    {
        if (isLeftSibling)
        {
            var separatorIndex = childIndex - 1;
            if (child.IsLeaf)
            {
                var borrowedKey = sibling.Keys[^1];
                var borrowedPartition = sibling.Partitions[^1];
                sibling.Keys.RemoveAt(sibling.Keys.Count - 1);
                sibling.Partitions.RemoveAt(sibling.Partitions.Count - 1);

                child.Keys.Insert(0, borrowedKey);
                child.Partitions.Insert(0, borrowedPartition);

                parent.Keys[separatorIndex] = child.Keys[0];
            }
            else // Internal node
            {
                var parentKey = parent.Keys[separatorIndex];
                child.Keys.Insert(0, parentKey);
                parent.Keys[separatorIndex] = sibling.Keys[^1];
                sibling.Keys.RemoveAt(sibling.Keys.Count - 1);

                var borrowedChildOffset = sibling.ChildrenOffsets[^1];
                sibling.ChildrenOffsets.RemoveAt(sibling.ChildrenOffsets.Count - 1);
                child.ChildrenOffsets.Insert(0, borrowedChildOffset);
            }
        }
        else // Right Sibling
        {
            var separatorIndex = childIndex;
            if (child.IsLeaf)
            {
                var borrowedKey = sibling.Keys[0];
                var borrowedPartition = sibling.Partitions[0];
                sibling.Keys.RemoveAt(0);
                sibling.Partitions.RemoveAt(0);

                child.Keys.Add(borrowedKey);
                child.Partitions.Add(borrowedPartition);

                parent.Keys[separatorIndex] = sibling.Keys[0];
            }
            else // Internal node
            {
                var parentKey = parent.Keys[separatorIndex];
                child.Keys.Add(parentKey);
                parent.Keys[separatorIndex] = sibling.Keys[0];
                sibling.Keys.RemoveAt(0);

                var borrowedChildOffset = sibling.ChildrenOffsets[0];
                sibling.ChildrenOffsets.RemoveAt(0);
                child.ChildrenOffsets.Add(borrowedChildOffset);
            }
        }
        return Task.CompletedTask;
    }

    private Task<BPlusTreeNode> MergeWithSiblingAsync(BPlusTreeNode parent, int childIndex, BPlusTreeNode child, BPlusTreeNode sibling, bool isLeftSibling)
    {
        if (isLeftSibling)
        {
            var separatorIndex = childIndex - 1;
            var separatorKey = parent.Keys[separatorIndex];

            if (child.IsLeaf)
            {
                sibling.Keys.AddRange(child.Keys);
                sibling.Partitions.AddRange(child.Partitions);
            }
            else // Internal node
            {
                sibling.Keys.Add(separatorKey);
                sibling.Keys.AddRange(child.Keys);
                sibling.ChildrenOffsets.AddRange(child.ChildrenOffsets);
            }

            parent.Keys.RemoveAt(separatorIndex);
            return Task.FromResult(sibling);
        }
        else // Merge child with right sibling
        {
            var separatorIndex = childIndex;
            var separatorKey = parent.Keys[separatorIndex];

            if (child.IsLeaf)
            {
                child.Keys.AddRange(sibling.Keys);
                child.Partitions.AddRange(sibling.Partitions);
            }
            else // Internal node
            {
                child.Keys.Add(separatorKey);
                child.Keys.AddRange(sibling.Keys);
                child.ChildrenOffsets.AddRange(sibling.ChildrenOffsets);
            }

            parent.Keys.RemoveAt(separatorIndex);
            return Task.FromResult(child);
        }
    }

    private async Task<long> InsertNonFullAsync(BPlusTreeNode node, long nodeOffset, Partition partition)
    {
        var key = partition.StartKey;
        if (node.IsLeaf)
        {
            int i = 0;
            while (i < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[i]) > 0)
            {
                i++;
            }
            node.Keys.Insert(i, key);
            node.Partitions.Insert(i, partition);
            
            // A leaf node was modified, so it must be written to a new location.
            return await AllocateAndWriteNodeAsync(node);
        }
        else
        {
            int i = 0;
            while (i < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[i]) > 0)
            {
                i++;
            }
            var childOffset = node.ChildrenOffsets[i];
            var childNode = await serializationHelper.ReadNodeAsync(indexStream!, childOffset);
            bool parentNeedsReallocation = false;

            if (childNode.Keys.Count == (2 * header.Degree - 1))
            {
                await SplitChildAsync(node, i, childNode, childOffset);
                parentNeedsReallocation = true; // The parent node has grown.

                if (Comparer<object>.Default.Compare(key, node.Keys[i]) > 0)
                {
                    i++;
                }
            }
            
            var childToInsertInOffset = node.ChildrenOffsets[i];
            var childToInsertIn = await serializationHelper.ReadNodeAsync(indexStream!, childToInsertInOffset);
            var newChildOffset = await InsertNonFullAsync(childToInsertIn, childToInsertInOffset, partition);

            if (newChildOffset != childToInsertInOffset)
            {
                node.ChildrenOffsets[i] = newChildOffset;
                parentNeedsReallocation = true; // A child pointer changed.
            }

            if (parentNeedsReallocation)
            {
                // Parent node was modified (split or child pointer update), so it must be re-allocated.
                return await AllocateAndWriteNodeAsync(node);
            }
            
            return nodeOffset;
        }
    }

    private async Task SplitChildAsync(BPlusTreeNode parentNode, int childIndex, BPlusTreeNode childNode, long childOffset)
    {
        var newSiblingNode = new BPlusTreeNode { IsLeaf = childNode.IsLeaf };
        int t = header.Degree;

        // B-Tree split logic (not a pure B+ Tree, but consistent with original intent).
        // The middle key is moved up to the parent.
        var middleKey = childNode.Keys[t - 1];
        parentNode.Keys.Insert(childIndex, middleKey);

        if (childNode.IsLeaf)
        {
            // The middle key and all subsequent keys/partitions move to the new sibling.
            newSiblingNode.Keys.AddRange(childNode.Keys.GetRange(t - 1, childNode.Keys.Count - (t - 1)));
            newSiblingNode.Partitions.AddRange(childNode.Partitions.GetRange(t - 1, childNode.Partitions.Count - (t - 1)));

            childNode.Keys.RemoveRange(t - 1, childNode.Keys.Count - (t - 1));
            childNode.Partitions.RemoveRange(t - 1, childNode.Partitions.Count - (t - 1));
        }
        else // Internal node
        {
            // Middle key is already moved up. Subsequent keys and children move to the new sibling.
            newSiblingNode.Keys.AddRange(childNode.Keys.GetRange(t, childNode.Keys.Count - t));
            newSiblingNode.ChildrenOffsets.AddRange(childNode.ChildrenOffsets.GetRange(t, childNode.ChildrenOffsets.Count - t));

            childNode.Keys.RemoveRange(t - 1, childNode.Keys.Count - (t - 1));
            childNode.ChildrenOffsets.RemoveRange(t, childNode.ChildrenOffsets.Count - t);
        }

        var newSiblingOffset = await AllocateAndWriteNodeAsync(newSiblingNode);
        parentNode.ChildrenOffsets.Insert(childIndex + 1, newSiblingOffset);
        
        // The original child node is now smaller. Overwriting it in-place is safe.
        await serializationHelper.WriteNodeAsync(indexStream!, childNode, childOffset);
    }
    
    private async Task<Partition?> FindInNodeAsync(long nodeOffset, object key)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream!, nodeOffset);

        if (node.IsLeaf)
        {
            // In a B+ tree, all data is in leaves. We find the partition whose start_key <= key.
            // We iterate backward to find the last key that is less than or equal to the search key.
            for (int i = node.Keys.Count - 1; i >= 0; i--)
            {
                if (Comparer<object>.Default.Compare(key, node.Keys[i]) >= 0)
                {
                    return node.Partitions[i];
                }
            }
            return null;
        }

        // Find the child to descend into.
        int childIndex = 0;
        while (childIndex < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }
        return await FindInNodeAsync(node.ChildrenOffsets[childIndex], key);
    }
    
    private async Task<(BPlusTreeNode Node, long Offset)> FindLeafNodeAsync(long nodeOffset, object key)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream!, nodeOffset);

        if (node.IsLeaf)
        {
            return (node, nodeOffset);
        }

        // Find the child to descend into.
        int childIndex = 0;
        while (childIndex < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }
        return await FindLeafNodeAsync(node.ChildrenOffsets[childIndex], key);
    }

    private async Task<long> UpdateInNodeAsync(long nodeOffset, Partition partition)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream!, nodeOffset);
        var key = partition.StartKey;

        if (node.IsLeaf)
        {
            // We are in a leaf node. Find and update the partition.
            int index = node.Keys.FindIndex(k => Comparer<object>.Default.Compare(k, key) == 0);
            if (index != -1)
            {
                // Found it. Update the partition data.
                node.Partitions[index] = partition;

                // Since the node has been modified, it must be re-allocated to a new location in the stream.
                // This correctly handles cases where the serialized size of the node changes.
                return await AllocateAndWriteNodeAsync(node);
            }
            else
            {
                // The key was not found in the leaf where it should exist.
                throw new KeyNotFoundException($"Could not find a partition with start key '{partition.StartKey}' to update.");
            }
        }
        else // This is an internal node.
        {
            // Find the child to descend into.
            int childIndex = 0;
            while (childIndex < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[childIndex]) >= 0)
            {
                childIndex++;
            }

            long childOffset = node.ChildrenOffsets[childIndex];
            
            // Recursively call update on the appropriate child.
            long newChildOffset = await UpdateInNodeAsync(childOffset, partition);

            // If the child's offset changed, it means it was re-allocated.
            // The parent (this node) must be updated with the new child offset.
            if (newChildOffset != childOffset)
            {
                node.ChildrenOffsets[childIndex] = newChildOffset;

                // Since this node is now modified (its child pointer changed),
                // it also needs to be re-allocated.
                return await AllocateAndWriteNodeAsync(node);
            }
            
            // If the child was not re-allocated, this node does not need to be either.
            return nodeOffset;
        }
    }

    private async Task<long> AllocateAndWriteNodeAsync(BPlusTreeNode node)
    {
        var offset = header.NextAvailableOffset;
        var writtenBytes = await serializationHelper.WriteNodeAsync(indexStream!, node, offset);
        header = header with { NextAvailableOffset = offset + writtenBytes };
        return offset;
    }
}