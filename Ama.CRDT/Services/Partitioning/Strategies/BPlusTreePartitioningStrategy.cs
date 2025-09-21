namespace Ama.CRDT.Services.Partitioning.Strategies;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// An implementation of <see cref="IPartitioningStrategy"/> that uses a B+ Tree to index partitions based on their start key.
/// The tree is stored and managed within a provided index stream.
/// </summary>
public sealed class BPlusTreePartitioningStrategy(
    IIndexSerializationHelper serializationHelper,
    IPartitionStreamProvider streamProvider,
    BPlusTreeCrdtMetrics metrics) : IPartitioningStrategy
{
    private const int HeaderSize = 1024; // Reserve 1KB for header to allow for future expansion.

    // Cache for B+ Tree nodes
    private readonly Dictionary<long, LinkedListNode<KeyValuePair<long, BPlusTreeNode>>> nodeCache = new();
    private readonly LinkedList<KeyValuePair<long, BPlusTreeNode>> lruList = new();
    private const int MaxCacheSize = 100;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        using var _ = new MetricTimer(metrics.InitializationDuration);
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        if (indexStream.Length > 0)
        {
            // Stream is not empty, assume it's an existing index and just load its header to validate.
            await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        }
        else
        {
            // Stream is empty, so create a new index.
            var header = new BTreeHeader(NextAvailableOffset: HeaderSize, PartitionCount: 0);
        
            // Set stream length for the header.
            indexStream.SetLength(HeaderSize);

            var root = new BPlusTreeNode { IsLeaf = true };
            var (rootOffset, newHeader) = await AllocateAndWriteNodeAsync(indexStream, header, root);
        
            header = newHeader with { RootNodeOffset = rootOffset };
            await serializationHelper.WriteHeaderAsync(indexStream, header, HeaderSize);
        }
    }

    /// <inheritdoc/>
    public async Task<IPartition?> FindPartitionAsync(CompositePartitionKey key)
    {
        using var _ = new MetricTimer(metrics.FindDuration);
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
    public async Task InsertPartitionAsync(IPartition partition)
    {
        using var _ = new MetricTimer(metrics.InsertDuration);
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Strategy has not been initialized correctly, root node is missing.");
        }

        metrics.NodeReads.Add(1);
        var root = await ReadNodeAsync(indexStream, header.RootNodeOffset);
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
    public async Task UpdatePartitionAsync(IPartition partition)
    {
        using var _ = new MetricTimer(metrics.UpdateDuration);
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Strategy has not been initialized correctly, root node is missing.");
        }
        
        var (newRootOffset, newHeader) = await UpdateInNodeAsync(indexStream, header, header.RootNodeOffset, partition);
        var headerToWrite = newHeader with { RootNodeOffset = newRootOffset };
        await serializationHelper.WriteHeaderAsync(indexStream, headerToWrite, HeaderSize);
    }

    /// <inheritdoc/>
    public async Task DeletePartitionAsync(IPartition partition)
    {
        using var _ = new MetricTimer(metrics.DeleteDuration);
        var indexStream = await streamProvider.GetIndexStreamAsync();

        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            throw new InvalidOperationException("Cannot delete from an empty tree.");
        }
        
        var (newRootOffset, finalHeader) = await DeleteRecursiveAsync(indexStream, header, header.RootNodeOffset, partition);

        metrics.NodeReads.Add(1);
        var root = await ReadNodeAsync(indexStream, newRootOffset);

        // If root becomes an internal node with no keys and one child, the child becomes the new root.
        if (!root.IsLeaf && root.Keys.Count == 0 && root.ChildrenOffsets.Count == 1)
        {
            newRootOffset = root.ChildrenOffsets[0];
        }

        var headerToWrite = finalHeader with { RootNodeOffset = newRootOffset };
        await serializationHelper.WriteHeaderAsync(indexStream, headerToWrite, HeaderSize);
    }
    
    /// <inheritdoc/>
    public async IAsyncEnumerable<IPartition> GetAllPartitionsAsync(IComparable? logicalKey = null)
    {
        using var _ = new MetricTimer(metrics.GetAllDuration);
        var indexStream = await streamProvider.GetIndexStreamAsync();

        if (indexStream.Length <= HeaderSize)
        {
            yield break;
        }

        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            yield break;
        }
        
        if (logicalKey is null)
        {
            await foreach (var partition in StreamAllPartitionsRecursiveAsync(indexStream, header.RootNodeOffset))
            {
                yield return partition;
            }
            yield break;
        }

        var startKey = new CompositePartitionKey(logicalKey, null);
        long? firstLeafOffset = await FindLeafNodeOffsetForKeyAsync(indexStream, header.RootNodeOffset, startKey);
        
        if (firstLeafOffset is null)
        {
            yield break;
        }
        
        long currentLeafOffset = firstLeafOffset.Value;
        while(currentLeafOffset != -1)
        {
            var leafNode = await ReadNodeAsync(indexStream, currentLeafOffset);
            foreach (var partition in leafNode.Partitions)
            {
                var pKey = partition.GetPartitionKey();
                if (pKey.LogicalKey.Equals(logicalKey))
                {
                    yield return partition;
                }
                else if (pKey.LogicalKey.CompareTo(logicalKey) > 0)
                {
                    yield break;
                }
            }
            currentLeafOffset = leafNode.NextLeafOffset;
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetPartitionCountAsync(IComparable? logicalKey = null)
    {
        using var _ = new MetricTimer(metrics.GetPartitionCountDuration);
        var indexStream = await streamProvider.GetIndexStreamAsync();
        if (indexStream.Length == 0)
        {
            return 0;
        }

        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (logicalKey is null)
        {
            return header.PartitionCount;
        }

        long count = 0;
        await foreach (var partition in GetAllPartitionsAsync(logicalKey))
        {
            count++;
        }
        return count;
    }
    
    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionByIndexAsync(IComparable logicalKey, long index)
    {
        using var _ = new MetricTimer(metrics.GetDataPartitionByIndexDuration);
        if (index < 0)
        {
            return null;
        }

        var indexStream = await streamProvider.GetIndexStreamAsync();
        if (indexStream.Length <= HeaderSize)
        {
            return null;
        }

        var header = await serializationHelper.ReadHeaderAsync(indexStream, HeaderSize);
        if (header.RootNodeOffset == -1)
        {
            return null;
        }

        var startKey = new CompositePartitionKey(logicalKey, null);
        long? firstLeafOffset = await FindLeafNodeOffsetForKeyAsync(indexStream, header.RootNodeOffset, startKey);
        
        if (firstLeafOffset is null)
        {
            return null;
        }

        long currentIndex = -1;
        long currentLeafOffset = firstLeafOffset.Value;
        
        while (currentLeafOffset != -1)
        {
            var leafNode = await ReadNodeAsync(indexStream, currentLeafOffset);
            
            foreach (var partition in leafNode.Partitions)
            {
                var partitionKey = partition.GetPartitionKey();

                if (partitionKey.LogicalKey.CompareTo(logicalKey) > 0)
                {
                    return null;
                }

                if (partition is DataPartition && partitionKey.LogicalKey.Equals(logicalKey))
                {
                    currentIndex++;
                    if (currentIndex == index)
                    {
                        return partition;
                    }
                }
            }
            currentLeafOffset = leafNode.NextLeafOffset;
        }

        return null;
    }

    private async IAsyncEnumerable<IPartition> StreamAllPartitionsRecursiveAsync(Stream indexStream, long nodeOffset)
    {
        if (nodeOffset == -1)
        {
            yield break;
        }

        metrics.NodeReads.Add(1);
        var node = await ReadNodeAsync(indexStream, nodeOffset);

        if (node.IsLeaf)
        {
            foreach (var partition in node.Partitions)
            {
                yield return partition;
            }
        }
        else
        {
            foreach (var childOffset in node.ChildrenOffsets)
            {
                await foreach (var partition in StreamAllPartitionsRecursiveAsync(indexStream, childOffset))
                {
                    yield return partition;
                }
            }
        }
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> DeleteRecursiveAsync(Stream indexStream, BTreeHeader header, long nodeOffset, IPartition partitionToDelete)
    {
        metrics.NodeReads.Add(1);
        var node = await ReadNodeAsync(indexStream, nodeOffset);
        int t = header.Degree;
        bool nodeModified = false;
        var currentHeader = header;
        var key = partitionToDelete.GetPartitionKey();

        if (node.IsLeaf)
        {
            bool isDeletingHeader = partitionToDelete is HeaderPartition;
            int keyIndex = node.Partitions.FindIndex(p => p.GetPartitionKey().CompareTo(key) == 0 && p is HeaderPartition == isDeletingHeader);
            
            if (keyIndex == -1)
            {
                throw new KeyNotFoundException($"Could not find a partition with key '{key}' to delete.");
            }

            node.Keys.RemoveAt(keyIndex);
            node.Partitions.RemoveAt(keyIndex);
            currentHeader = currentHeader with { PartitionCount = currentHeader.PartitionCount - 1 };
            (nodeOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
            return (nodeOffset, currentHeader);
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }

        var childOffset = node.ChildrenOffsets[childIndex];
        metrics.NodeReads.Add(1);
        var childNode = await ReadNodeAsync(indexStream, childOffset);
        
        if (childNode.Keys.Count < t)
        {
            if (childIndex > 0)
            {
                var leftSiblingOffset = node.ChildrenOffsets[childIndex - 1];
                metrics.NodeReads.Add(1);
                var leftSibling = await ReadNodeAsync(indexStream, leftSiblingOffset);
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
                metrics.NodeReads.Add(1);
                var rightSibling = await ReadNodeAsync(indexStream, rightSiblingOffset);
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

        var (newChildOffset, newChildHeader) = await DeleteRecursiveAsync(indexStream, currentHeader, childOffset, partitionToDelete);
        currentHeader = newChildHeader;
        if (newChildOffset != childOffset)
        {
            node.ChildrenOffsets[childIndex] = newChildOffset;
            nodeModified = true;
        }
        
        if (childIndex > 0)
        {
            var firstKeyInChildSubtree = await GetFirstKeyOfSubtree(indexStream, node.ChildrenOffsets[childIndex]);
            if (firstKeyInChildSubtree != null && node.Keys[childIndex - 1].CompareTo(firstKeyInChildSubtree) != 0)
            {
                node.Keys[childIndex - 1] = firstKeyInChildSubtree;
                nodeModified = true;
            }
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
        metrics.NodesBorrowed.Add(1);
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

                if (sibling.Keys.Count > 0)
                {
                    parent.Keys[separatorIndex] = sibling.Keys[0];
                }
                else
                {
                }

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
        metrics.NodesMerged.Add(1);
        if (isLeftSibling)
        {
            var separatorIndex = childIndex - 1;
            var separatorKey = parent.Keys[separatorIndex];

            if (child.IsLeaf)
            {
                sibling.Keys.AddRange(child.Keys);
                sibling.Partitions.AddRange(child.Partitions);
                sibling.NextLeafOffset = child.NextLeafOffset;
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
                child.NextLeafOffset = sibling.NextLeafOffset;
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

    private async Task<(long newOffset, BTreeHeader newHeader)> InsertNonFullAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode node, long nodeOffset, IPartition partition)
    {
        var key = partition.GetPartitionKey();
        var currentHeader = header;
        
        if (node.IsLeaf)
        {
            int i = 0;
            while (i < node.Keys.Count && key.CompareTo(node.Keys[i]) > 0) i++;
            node.Keys.Insert(i, key);
            node.Partitions.Insert(i, partition);
            
            currentHeader = currentHeader with { PartitionCount = currentHeader.PartitionCount + 1 };
            (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
            return (newOffset, currentHeader);
        }
        else
        {
            int i = 0;
            while (i < node.Keys.Count && key.CompareTo(node.Keys[i]) > 0) i++;
            
            var childOffset = node.ChildrenOffsets[i];
            metrics.NodeReads.Add(1);
            var childNode = await ReadNodeAsync(indexStream, childOffset);
            bool parentNeedsReallocation = false;

            if (childNode.Keys.Count == (2 * currentHeader.Degree - 1))
            {
                currentHeader = await SplitChildAsync(indexStream, currentHeader, node, i, childNode, childOffset);
                parentNeedsReallocation = true;

                if (key.CompareTo(node.Keys[i]) > 0)
                {
                    i++;
                }
            }
            
            var childToInsertInOffset = node.ChildrenOffsets[i];
            metrics.NodeReads.Add(1);
            var childToInsertIn = await ReadNodeAsync(indexStream, childToInsertInOffset);
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
        metrics.NodesSplit.Add(1);
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
            
            newSiblingNode.NextLeafOffset = childNode.NextLeafOffset;
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
        
        if (childNode.IsLeaf)
        {
            childNode.NextLeafOffset = newSiblingOffset;
        }

        parentNode.ChildrenOffsets.Insert(childIndex + 1, newSiblingOffset);
        
        metrics.NodeWrites.Add(1);
        await serializationHelper.WriteNodeAsync(indexStream, childNode, childOffset);
        AddToCache(childOffset, childNode);
        return currentHeader;
    }

    private async Task<long?> FindLeafNodeOffsetForKeyAsync(Stream indexStream, long nodeOffset, CompositePartitionKey key)
    {
        metrics.NodeReads.Add(1);
        var node = await ReadNodeAsync(indexStream, nodeOffset);

        if (node.IsLeaf)
        {
            return nodeOffset;
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }
        return await FindLeafNodeOffsetForKeyAsync(indexStream, node.ChildrenOffsets[childIndex], key);
    }
    
    private async Task<IPartition?> FindInNodeAsync(Stream indexStream, long nodeOffset, CompositePartitionKey key)
    {
        metrics.NodeReads.Add(1);
        var node = await ReadNodeAsync(indexStream, nodeOffset);

        if (node.IsLeaf)
        {
            IPartition? candidatePartition = null;

            for (int i = node.Keys.Count - 1; i >= 0; i--)
            {
                if (key.CompareTo(node.Keys[i]) >= 0)
                {
                    candidatePartition = node.Partitions[i];
                    break;
                }
            }

            if (candidatePartition is not null && EqualityComparer<object>.Default.Equals(candidatePartition.GetPartitionKey().LogicalKey, key.LogicalKey))
            {
                if (key.RangeKey is null && candidatePartition is not HeaderPartition)
                {
                    return null;
                }
                return candidatePartition;
            }
            return null;
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0)
        {
            childIndex++;
        }
        return await FindInNodeAsync(indexStream, node.ChildrenOffsets[childIndex], key);
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> UpdateInNodeAsync(Stream indexStream, BTreeHeader header, long nodeOffset, IPartition partition)
    {
        metrics.NodeReads.Add(1);
        var node = await ReadNodeAsync(indexStream, nodeOffset);
        var key = partition.GetPartitionKey();
        var currentHeader = header;

        if (node.IsLeaf)
        {
            bool isUpdatingHeader = partition is HeaderPartition;
            int indexToUpdate = node.Partitions.FindIndex(p => p.GetPartitionKey().CompareTo(key) == 0 && p is HeaderPartition == isUpdatingHeader);

            if (indexToUpdate != -1)
            {
                node.Partitions[indexToUpdate] = partition;
                (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node);
                return (newOffset, currentHeader);
            }
            else
            {
                throw new KeyNotFoundException($"Could not find a partition with key '{partition.GetPartitionKey()}' to update.");
            }
        }
        else
        {
            int childIndex = 0;
            while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0)
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
        metrics.NodeWrites.Add(1);
        var writtenBytes = await serializationHelper.WriteNodeAsync(indexStream, node, offset);
        AddToCache(offset, node);
        var newHeader = header with { NextAvailableOffset = offset + writtenBytes };
        return (offset, newHeader);
    }
    
    private async Task<IComparable?> GetFirstKeyOfSubtree(Stream stream, long nodeOffset)
    {
        metrics.NodeReads.Add(1);
        var node = await ReadNodeAsync(stream, nodeOffset);
        if (node.IsLeaf)
        {
            return node.Keys.Count > 0 ? node.Keys[0] : null;
        }
        
        return node.ChildrenOffsets.Count > 0 
            ? await GetFirstKeyOfSubtree(stream, node.ChildrenOffsets[0]) 
            : null;
    }

    private bool TryGetFromCache(long offset, out BPlusTreeNode? node)
    {
        if (nodeCache.TryGetValue(offset, out var linkedListNode))
        {
            node = linkedListNode.Value.Value;
            lruList.Remove(linkedListNode);
            lruList.AddFirst(linkedListNode);
            return true;
        }
        node = null;
        return false;
    }

    private void AddToCache(long offset, BPlusTreeNode node)
    {
        if (nodeCache.TryGetValue(offset, out var existingNode))
        {
            lruList.Remove(existingNode);
            nodeCache.Remove(offset);
        }

        if (nodeCache.Count >= MaxCacheSize)
        {
            var lru = lruList.Last;
            if (lru != null)
            {
                lruList.RemoveLast();
                nodeCache.Remove(lru.Value.Key);
            }
        }
    
        var newNode = new LinkedListNode<KeyValuePair<long, BPlusTreeNode>>(new KeyValuePair<long, BPlusTreeNode>(offset, node));
        lruList.AddFirst(newNode);
        nodeCache[offset] = newNode;
    }

    private async Task<BPlusTreeNode> ReadNodeAsync(Stream indexStream, long nodeOffset)
    {
        if (TryGetFromCache(nodeOffset, out var cachedNode))
        {
            return cachedNode!;
        }

        var node = await serializationHelper.ReadNodeAsync(indexStream, nodeOffset);
        AddToCache(nodeOffset, node);
        return node;
    }
}