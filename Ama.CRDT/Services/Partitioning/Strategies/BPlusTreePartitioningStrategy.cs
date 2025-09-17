namespace Ama.CRDT.Services.Partitioning.Strategies;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Partitioning.Serialization;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// An implementation of <see cref="IPartitioningStrategy"/> that uses a B+ Tree to index partitions based on their start key.
/// The tree is stored and managed within a provided index stream.
/// </summary>
public sealed class BPlusTreePartitioningStrategy(IIndexSerializationHelper serializationHelper, IPartitionStreamProvider streamProvider) : IPartitioningStrategy
{
    private const int HeaderSize = 1024; // Reserve 1KB for header to allow for future expansion.

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        if (indexStream.Length > 0)
        {
            // Stream is not empty, assume it's an existing index and just load its header to validate.
            await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        }
        else
        {
            // Stream is empty, so create a new index.
            var header = new BTreeHeader(NextAvailableOffset: HeaderSize); // Data starts after the header
        
            // Set stream length for the header.
            indexStream.SetLength(HeaderSize);

            var root = new BPlusTreeNode { IsLeaf = true };
            var (rootOffset, newHeader) = await AllocateAndWriteNodeAsync(indexStream, header, root);
        
            header = newHeader with { RootNodeOffset = rootOffset };
            await serializationHelper.WriteHeaderAsync(indexStream, header, HeaderSize);
        }
    }

    /// <inheritdoc/>
    public async Task<Partition?> FindPartitionAsync(CompositePartitionKey key)
    {
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        if (indexStream.Length <= HeaderSize)
        {
            return null; // No data besides a potentially empty header, so no partitions.
        }
        
        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        
        if (header.RootNodeOffset == -1)
        {
             return null;
        }
        
        return await FindInNodeAsync(indexStream, header.RootNodeOffset, key);
    }

    /// <inheritdoc/>
    public async Task InsertPartitionAsync(Partition partition)
    {
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Strategy has not been initialized correctly, root node is missing.");
        }

        var root = await serializationHelper.ReadNodeAsync(indexStream, header.RootNodeOffset);
        if (root.Keys.Count == (2 * header.Degree - 1)) // Root is full
        {
            var oldRootOffset = header.RootNodeOffset;
            var newRoot = new BPlusTreeNode();
            newRoot.ChildrenOffsets.Add(oldRootOffset);

            header = await SplitChildAsync(indexStream, header, newRoot, 0, root, oldRootOffset);
            
            var (newRootOffset, newHeader) = await AllocateAndWriteNodeAsync(indexStream, header, newRoot);
            header = newHeader;
            header = header with { RootNodeOffset = newRootOffset };
            
            var (finalRootOffset, finalHeader) = await InsertNonFullAsync(indexStream, header, newRoot, newRootOffset, partition);
            header = finalHeader with { RootNodeOffset = finalRootOffset };
        }
        else
        {
            var (newRootOffset, newHeader) = await InsertNonFullAsync(indexStream, header, root, header.RootNodeOffset, partition);
            header = newHeader with { RootNodeOffset = newRootOffset };
        }
        await serializationHelper.WriteHeaderAsync(indexStream, header, HeaderSize);
    }

    /// <inheritdoc/>
    public async Task UpdatePartitionAsync(Partition partition)
    {
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Strategy has not been initialized correctly, root node is missing.");
        }
        
        var (newRootOffset, _) = await UpdateInNodeAsync(indexStream, header, header.RootNodeOffset, partition);

        if (newRootOffset != header.RootNodeOffset)
        {
            header = header with { RootNodeOffset = newRootOffset };
            await serializationHelper.WriteHeaderAsync(indexStream, header, HeaderSize);
        }
    }

    /// <inheritdoc/>
    public async Task DeletePartitionAsync(Partition partition)
    {
        var key = partition.StartKey;

        var indexStream = await streamProvider.GetIndexStreamAsync();

        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Cannot delete from an empty tree.");
        }
        
        var (newRootOffset, newHeader) = await DeleteRecursiveAsync(indexStream, header, header.RootNodeOffset, key);
        header = newHeader;

        var root = await serializationHelper.ReadNodeAsync(indexStream, newRootOffset);

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
        var indexStream = await streamProvider.GetIndexStreamAsync();

        if (indexStream.Length <= HeaderSize)
        {
            return [];
        }

        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            return [];
        }

        var allPartitions = new List<Partition>();
        await GetAllPartitionsRecursiveAsync(indexStream, header.RootNodeOffset, allPartitions);

        allPartitions.Sort((p1, p2) => Comparer<object>.Default.Compare(p1.StartKey, p2.StartKey));

        return allPartitions;
    }

    private async Task GetAllPartitionsRecursiveAsync(Stream indexStream, long nodeOffset, List<Partition> partitions)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream, nodeOffset);
        if (node.IsLeaf)
        {
            partitions.AddRange(node.Partitions);
        }
        else
        {
            foreach (var childOffset in node.ChildrenOffsets)
            {
                await GetAllPartitionsRecursiveAsync(indexStream, childOffset, partitions);
            }
        }
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> DeleteRecursiveAsync(Stream indexStream, BTreeHeader header, long nodeOffset, CompositePartitionKey key)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream, nodeOffset);
        int t = header.Degree;
        bool nodeModified = false;
        var currentHeader = header;

        if (node.IsLeaf)
        {
            int keyIndex = node.Keys.FindIndex(k => Comparer<object>.Default.Compare(k, key) == 0);
            if (keyIndex == -1)
            {
                throw new KeyNotFoundException($"Could not find a partition with start key '{key}' to delete.");
            }

            node.Keys.RemoveAt(keyIndex);
            node.Partitions.RemoveAt(keyIndex);
            (nodeOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
            return (nodeOffset, currentHeader);
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }

        var childOffset = node.ChildrenOffsets[childIndex];
        var childNode = await serializationHelper.ReadNodeAsync(indexStream, childOffset);
        
        if (childNode.Keys.Count < t)
        {
            if (childIndex > 0)
            {
                var leftSiblingOffset = node.ChildrenOffsets[childIndex - 1];
                var leftSibling = await serializationHelper.ReadNodeAsync(indexStream, leftSiblingOffset);
                if (leftSibling.Keys.Count >= t)
                {
                    BorrowFromSibling(node, childIndex, childNode, leftSibling, isLeftSibling: true);
                    (var newLeftSiblingOffset, var h1) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, leftSibling);
                    node.ChildrenOffsets[childIndex - 1] = newLeftSiblingOffset;
                    (childOffset, var h2) = await AllocateAndWriteNodeAsync(indexStream, h1, childNode);
                    currentHeader = h2;
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else
                {
                    var mergedNode = MergeWithSibling(node, childIndex, childNode, leftSibling, isLeftSibling: true);
                    childIndex--;
                    (childOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, mergedNode);
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
            else if (childIndex + 1 < node.ChildrenOffsets.Count)
            {
                var rightSiblingOffset = node.ChildrenOffsets[childIndex + 1];
                var rightSibling = await serializationHelper.ReadNodeAsync(indexStream, rightSiblingOffset);
                if (rightSibling.Keys.Count >= t)
                {
                    BorrowFromSibling(node, childIndex, childNode, rightSibling, isLeftSibling: false);
                    (var newRightSiblingOffset, var h1) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, rightSibling);
                    node.ChildrenOffsets[childIndex + 1] = newRightSiblingOffset;
                    (childOffset, var h2) = await AllocateAndWriteNodeAsync(indexStream, h1, childNode);
                    currentHeader = h2;
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else
                {
                    var mergedNode = MergeWithSibling(node, childIndex, childNode, rightSibling, isLeftSibling: false);
                    (childOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, mergedNode);
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
        }

        var (newChildOffset, newChildHeader) = await DeleteRecursiveAsync(indexStream, currentHeader, childOffset, key);
        currentHeader = newChildHeader;
        if (newChildOffset != childOffset)
        {
            node.ChildrenOffsets[childIndex] = newChildOffset;
            nodeModified = true;
        }

        if (nodeModified)
        {
            (nodeOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
            return (nodeOffset, currentHeader);
        }

        return (nodeOffset, currentHeader);
    }
    
    private void BorrowFromSibling(BPlusTreeNode parent, int childIndex, BPlusTreeNode child, BPlusTreeNode sibling, bool isLeftSibling)
    {
        if (isLeftSibling)
        {
            var separatorIndex = childIndex - 1;
            if (child.IsLeaf)
            {
                child.Keys.Insert(0, sibling.Keys[^1]);
                child.Partitions.Insert(0, sibling.Partitions[^1]);
                sibling.Keys.RemoveAt(sibling.Keys.Count - 1);
                sibling.Partitions.RemoveAt(sibling.Partitions.Count - 1);

                parent.Keys[separatorIndex] = child.Keys[0];
            }
            else
            {
                child.Keys.Insert(0, parent.Keys[separatorIndex]);
                parent.Keys[separatorIndex] = sibling.Keys[^1];
                sibling.Keys.RemoveAt(sibling.Keys.Count - 1);

                child.ChildrenOffsets.Insert(0, sibling.ChildrenOffsets[^1]);
                sibling.ChildrenOffsets.RemoveAt(sibling.ChildrenOffsets.Count - 1);
            }
        }
        else
        {
            var separatorIndex = childIndex;
            if (child.IsLeaf)
            {
                child.Keys.Add(sibling.Keys[0]);
                child.Partitions.Add(sibling.Partitions[0]);
                sibling.Keys.RemoveAt(0);
                sibling.Partitions.RemoveAt(0);

                parent.Keys[separatorIndex] = sibling.Keys[0];
            }
            else
            {
                child.Keys.Add(parent.Keys[separatorIndex]);
                parent.Keys[separatorIndex] = sibling.Keys[0];
                sibling.Keys.RemoveAt(0);

                child.ChildrenOffsets.Add(sibling.ChildrenOffsets[0]);
                sibling.ChildrenOffsets.RemoveAt(0);
            }
        }
    }

    private BPlusTreeNode MergeWithSibling(BPlusTreeNode parent, int childIndex, BPlusTreeNode child, BPlusTreeNode sibling, bool isLeftSibling)
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
            else
            {
                sibling.Keys.Add(separatorKey);
                sibling.Keys.AddRange(child.Keys);
                sibling.ChildrenOffsets.AddRange(child.ChildrenOffsets);
            }

            parent.Keys.RemoveAt(separatorIndex);
            return sibling;
        }
        else
        {
            var separatorIndex = childIndex;
            var separatorKey = parent.Keys[separatorIndex];

            if (child.IsLeaf)
            {
                child.Keys.AddRange(sibling.Keys);
                child.Partitions.AddRange(sibling.Partitions);
            }
            else
            {
                child.Keys.Add(separatorKey);
                child.Keys.AddRange(sibling.Keys);
                child.ChildrenOffsets.AddRange(sibling.ChildrenOffsets);
            }

            parent.Keys.RemoveAt(separatorIndex);
            return child;
        }
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> InsertNonFullAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode node, long nodeOffset, Partition partition)
    {
        var key = partition.StartKey;
        var currentHeader = header;
        
        if (node.IsLeaf)
        {
            int i = 0;
            while (i < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[i]) > 0) i++;
            node.Keys.Insert(i, key);
            node.Partitions.Insert(i, partition);
            
            (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
            return (newOffset, currentHeader);
        }
        else
        {
            int i = 0;
            while (i < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[i]) > 0) i++;
            
            var childOffset = node.ChildrenOffsets[i];
            var childNode = await serializationHelper.ReadNodeAsync(indexStream, childOffset);
            bool parentNeedsReallocation = false;

            if (childNode.Keys.Count == (2 * currentHeader.Degree - 1))
            {
                currentHeader = await SplitChildAsync(indexStream, currentHeader, node, i, childNode, childOffset);
                parentNeedsReallocation = true;

                if (Comparer<object>.Default.Compare(key, node.Keys[i]) > 0)
                {
                    i++;
                }
            }
            
            var childToInsertInOffset = node.ChildrenOffsets[i];
            var childToInsertIn = await serializationHelper.ReadNodeAsync(indexStream, childToInsertInOffset);
            var (newChildOffset, newChildHeader) = await InsertNonFullAsync(indexStream, currentHeader, childToInsertIn, childToInsertInOffset, partition);
            currentHeader = newChildHeader;

            if (newChildOffset != childToInsertInOffset)
            {
                node.ChildrenOffsets[i] = newChildOffset;
                parentNeedsReallocation = true;
            }

            if (parentNeedsReallocation)
            {
                (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
                return (newOffset, currentHeader);
            }
            
            return (nodeOffset, currentHeader);
        }
    }

    private async Task<BTreeHeader> SplitChildAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode parentNode, int childIndex, BPlusTreeNode childNode, long childOffset)
    {
        var newSiblingNode = new BPlusTreeNode { IsLeaf = childNode.IsLeaf };
        int t = header.Degree;
        var currentHeader = header;

        var middleKey = childNode.Keys[t - 1];
        parentNode.Keys.Insert(childIndex, middleKey);

        if (childNode.IsLeaf)
        {
            newSiblingNode.Keys.AddRange(childNode.Keys.GetRange(t - 1, childNode.Keys.Count - (t - 1)));
            newSiblingNode.Partitions.AddRange(childNode.Partitions.GetRange(t - 1, childNode.Partitions.Count - (t - 1)));

            childNode.Keys.RemoveRange(t - 1, childNode.Keys.Count - (t - 1));
            childNode.Partitions.RemoveRange(t - 1, childNode.Partitions.Count - (t - 1));
        }
        else
        {
            newSiblingNode.Keys.AddRange(childNode.Keys.GetRange(t, childNode.Keys.Count - t));
            newSiblingNode.ChildrenOffsets.AddRange(childNode.ChildrenOffsets.GetRange(t, childNode.ChildrenOffsets.Count - t));

            childNode.Keys.RemoveRange(t - 1, childNode.Keys.Count - (t - 1));
            childNode.ChildrenOffsets.RemoveRange(t, childNode.ChildrenOffsets.Count - t);
        }

        var (newSiblingOffset, newHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, newSiblingNode);
        currentHeader = newHeader;
        parentNode.ChildrenOffsets.Insert(childIndex + 1, newSiblingOffset);
        
        await serializationHelper.WriteNodeAsync(indexStream, childNode, childOffset);
        return currentHeader;
    }
    
    private async Task<Partition?> FindInNodeAsync(Stream indexStream, long nodeOffset, CompositePartitionKey key)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream, nodeOffset);

        if (node.IsLeaf)
        {
            Partition? candidatePartition = null;
            for (int i = node.Keys.Count - 1; i >= 0; i--)
            {
                if (Comparer<object>.Default.Compare(key, node.Keys[i]) >= 0)
                {
                    candidatePartition = node.Partitions[i];
                    break;
                }
            }

            if (candidatePartition.HasValue &&
                EqualityComparer<object>.Default.Equals(candidatePartition.Value.StartKey.LogicalKey, key.LogicalKey))
            {
                return candidatePartition;
            }

            return null;
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }
        return await FindInNodeAsync(indexStream, node.ChildrenOffsets[childIndex], key);
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> UpdateInNodeAsync(Stream indexStream, BTreeHeader header, long nodeOffset, Partition partition)
    {
        var node = await serializationHelper.ReadNodeAsync(indexStream, nodeOffset);
        var key = partition.StartKey;
        var currentHeader = header;

        if (node.IsLeaf)
        {
            int index = node.Keys.FindIndex(k => Comparer<object>.Default.Compare(k, key) == 0);
            if (index != -1)
            {
                node.Partitions[index] = partition;
                (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
                return (newOffset, currentHeader);
            }
            else
            {
                throw new KeyNotFoundException($"Could not find a partition with start key '{partition.StartKey}' to update.");
            }
        }
        else
        {
            int childIndex = 0;
            while (childIndex < node.Keys.Count && Comparer<object>.Default.Compare(key, node.Keys[childIndex]) >= 0)
            {
                childIndex++;
            }

            long childOffset = node.ChildrenOffsets[childIndex];
            var (newChildOffset, newChildHeader) = await UpdateInNodeAsync(indexStream, currentHeader, childOffset, partition);
            currentHeader = newChildHeader;

            if (newChildOffset != childOffset)
            {
                node.ChildrenOffsets[childIndex] = newChildOffset;
                (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
                return (newOffset, currentHeader);
            }
            
            return (nodeOffset, currentHeader);
        }
    }

    private async Task<(long offset, BTreeHeader newHeader)> AllocateAndWriteNodeAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode node)
    {
        var offset = header.NextAvailableOffset;
        var writtenBytes = await serializationHelper.WriteNodeAsync(indexStream, node, offset);
        var newHeader = header with { NextAvailableOffset = offset + writtenBytes };
        return (offset, newHeader);
    }
}