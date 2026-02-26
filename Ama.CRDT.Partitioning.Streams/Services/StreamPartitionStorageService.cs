namespace Ama.CRDT.Partitioning.Streams.Services;

using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Partitioning.Streams.Services.Metrics;
using Ama.CRDT.Partitioning.Streams.Services.Serialization;
using Ama.CRDT.Services.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Partitioning.Streams.Models;

/// <summary>
/// An implementation of <see cref="IPartitionStorageService"/> that coordinates raw streams and an internal B+ Tree index
/// to persist and search partition data. This centralizes space allocation, caching, and serialization operations.
/// </summary>
public sealed class StreamPartitionStorageService : IPartitionStorageService
{
    private const int HeaderSize = 1024; // Reserve 1KB for B+ Tree header
    private const string HeaderIdentifier = "__HEADER__";
    private const int MaxCacheSize = 100;

    private readonly IPartitionStreamProvider streamProvider;
    private readonly IPartitionSerializationService serializationService;
    private readonly PartitionManagerCrdtMetrics metrics;
    private readonly StreamsCrdtMetrics treeMetrics;

    // Cache for B+ Tree nodes, keyed by (propertyName, offset) to support multiple index streams.
    private readonly Dictionary<(string, long), LinkedListNode<KeyValuePair<(string, long), BPlusTreeNode>>> nodeCache = new();
    private readonly LinkedList<KeyValuePair<(string, long), BPlusTreeNode>> lruList = new();

    public StreamPartitionStorageService(
        IServiceProvider serviceProvider,
        IPartitionSerializationService serializationService,
        PartitionManagerCrdtMetrics metrics,
        StreamsCrdtMetrics treeMetrics)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(serializationService);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(treeMetrics);

        this.streamProvider = serviceProvider.GetService<IPartitionStreamProvider>() ??
            throw new InvalidOperationException(
                $"No implementation for '{nameof(IPartitionStreamProvider)}' was found. " +
                $"When using partitioning features, you must register a custom stream provider. " +
                $"Use the 'services.AddCrdtPartitionStreamProvider<TProvider>()' extension method to register your implementation.");
        
        this.serializationService = serializationService;
        this.metrics = metrics;
        this.treeMetrics = treeMetrics;
    }

    #region Data Stream Operations (IPartitionStorageService core)

    /// <inheritdoc/>
    public async Task<CrdtDocument<TData>> LoadPartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partition, CancellationToken cancellationToken = default) where TData : class, new()
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);

        using var _ = new MetricTimer(metrics.StreamReadDuration);
        var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName, cancellationToken);
        return await LoadContentInternalAsync<TData>(partition, dataStream, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<TData>> LoadHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partition, CancellationToken cancellationToken = default) where TData : class, new()
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentNullException.ThrowIfNull(partition);

        using var _ = new MetricTimer(metrics.StreamReadDuration);
        var dataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey, cancellationToken);
        return await LoadContentInternalAsync<TData>(partition, dataStream, cancellationToken);
    }

    private async Task<CrdtDocument<TData>> LoadContentInternalAsync<TData>(IPartition partition, Stream dataStream, CancellationToken cancellationToken) where TData : class, new()
    {
        var docBuffer = new byte[partition.DataLength];
        dataStream.Seek(partition.DataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(docBuffer, cancellationToken);
        using var docStream = new MemoryStream(docBuffer);
        var doc = await serializationService.DeserializeObjectAsync<TData>(docStream, cancellationToken);
        
        var metaBuffer = new byte[partition.MetadataLength];
        dataStream.Seek(partition.MetadataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(metaBuffer, cancellationToken);
        using var metaStream = new MemoryStream(metaBuffer);
        var meta = await serializationService.DeserializeObjectAsync<CrdtMetadata>(metaStream, cancellationToken);

        return new CrdtDocument<TData>(doc!, meta!);
    }

    /// <inheritdoc/>
    public async Task<IPartition> SavePartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class, new()
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partitionToUpdate);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metadata);

        using var _ = new MetricTimer(metrics.StreamWriteDuration);
        var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName, cancellationToken);
        var header = await ReadOrCreateDataHeaderAsync(dataStream, cancellationToken);
        
        long oldDataOffset = partitionToUpdate is DataPartition dp1 && dp1.DataOffset > 0 ? dp1.DataOffset : -1;
        long oldDataLength = partitionToUpdate.DataLength;
        long oldMetaOffset = partitionToUpdate is DataPartition dp2 && dp2.MetadataOffset > 0 ? dp2.MetadataOffset : -1;
        long oldMetaLength = partitionToUpdate.MetadataLength;

        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, header, oldDataOffset, oldDataLength, cancellationToken);
        header = newDataWriteResult.Header;

        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, header, oldMetaOffset, oldMetaLength, cancellationToken);
        header = newMetaWriteResult.Header;

        await WriteDataHeaderAsync(dataStream, header, cancellationToken);

        return partitionToUpdate switch
        {
            DataPartition dp => dp with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length },
            _ => throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}")
        };
    }

    /// <inheritdoc/>
    public async Task<HeaderPartition> SaveHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class, new()
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentNullException.ThrowIfNull(partitionToUpdate);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metadata);

        using var _ = new MetricTimer(metrics.StreamWriteDuration);
        var dataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey, cancellationToken);
        var header = await ReadOrCreateDataHeaderAsync(dataStream, cancellationToken);
        
        long oldDataOffset = partitionToUpdate.DataOffset > 0 ? partitionToUpdate.DataOffset : -1;
        long oldDataLength = partitionToUpdate.DataLength;
        long oldMetaOffset = partitionToUpdate.MetadataOffset > 0 ? partitionToUpdate.MetadataOffset : -1;
        long oldMetaLength = partitionToUpdate.MetadataLength;

        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, header, oldDataOffset, oldDataLength, cancellationToken);
        header = newDataWriteResult.Header;

        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, header, oldMetaOffset, oldMetaLength, cancellationToken);
        header = newMetaWriteResult.Header;

        await WriteDataHeaderAsync(dataStream, header, cancellationToken);

        return partitionToUpdate with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length };
    }

    private async Task<(long Offset, long Length, DataStreamHeader Header)> WriteToStreamAsync(
        Stream dataStream, object content, DataStreamHeader header, long oldOffset, long oldLength, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await serializationService.SerializeObjectAsync(ms, content, cancellationToken);
        var requiredSize = ms.Length;

        var freeState = new FreeSpaceState(header.NextAvailableOffset, header.FreeBlocks);
        var (offsetToUse, newState) = StreamSpaceAllocator.Allocate(freeState, requiredSize, oldOffset, oldLength);

        dataStream.Seek(offsetToUse, SeekOrigin.Begin);
        ms.Seek(0, SeekOrigin.Begin);
        await ms.CopyToAsync(dataStream, cancellationToken);
        await dataStream.FlushAsync(cancellationToken);

        var newHeader = header with { NextAvailableOffset = newState.NextAvailableOffset, FreeBlocks = newState.FreeBlocks };
        return (offsetToUse, requiredSize, newHeader);
    }

    private async Task<DataStreamHeader> ReadOrCreateDataHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length < HeaderSize)
        {
            var newHeader = new DataStreamHeader();
            await WriteDataHeaderAsync(stream, newHeader, cancellationToken);
            return newHeader;
        }

        var buffer = new byte[HeaderSize];
        stream.Seek(0, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        var jsonString = Encoding.UTF8.GetString(buffer).TrimEnd();
        
        if (string.IsNullOrWhiteSpace(jsonString)) 
        {
            var newHeader = new DataStreamHeader();
            await WriteDataHeaderAsync(stream, newHeader, cancellationToken);
            return newHeader;
        }

        return JsonSerializer.Deserialize<DataStreamHeader>(jsonString) ?? new DataStreamHeader();
    }

    private async Task WriteDataHeaderAsync(Stream stream, DataStreamHeader header, CancellationToken cancellationToken)
    {
        var jsonString = JsonSerializer.Serialize(header);
        var buffer = Encoding.UTF8.GetBytes(jsonString);
        if (buffer.Length > HeaderSize) throw new InvalidOperationException($"Data stream header exceeded {HeaderSize} bytes.");

        var padded = new byte[HeaderSize];
        Array.Fill(padded, (byte)' ');
        Array.Copy(buffer, padded, buffer.Length);

        stream.Seek(0, SeekOrigin.Begin);
        await stream.WriteAsync(padded, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ClearPropertyDataAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var stream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName, cancellationToken);
        stream.SetLength(0);
        await ReadOrCreateDataHeaderAsync(stream, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ClearHeaderDataAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);

        var stream = await streamProvider.GetHeaderDataStreamAsync(logicalKey, cancellationToken);
        stream.SetLength(0);
        await ReadOrCreateDataHeaderAsync(stream, cancellationToken);
    }

    #endregion

    #region Index Stream Operations (Formerly IPartitioningStrategy)

    /// <inheritdoc/>
    public Task InitializePropertyIndexAsync(string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return InitializeInternalAsync(propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public Task InitializeHeaderIndexAsync(CancellationToken cancellationToken = default) 
        => InitializeInternalAsync(HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken);

    /// <inheritdoc/>
    public Task InsertPropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);
        return InsertPartitionInternalAsync(partition, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public Task InsertHeaderPartitionAsync(IComparable logicalKey, HeaderPartition headerPartition, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentNullException.ThrowIfNull(headerPartition);
        return InsertPartitionInternalAsync(headerPartition, HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdatePropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);
        return UpdatePartitionInternalAsync(partition, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateHeaderPartitionAsync(IComparable logicalKey, HeaderPartition headerPartition, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentNullException.ThrowIfNull(headerPartition);
        return UpdatePartitionInternalAsync(headerPartition, HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeletePropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);
        return DeletePartitionInternalAsync(partition, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IPartition> GetPartitionsAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return GetAllPartitionsInternalAsync(propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), logicalKey, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IPartition?> GetPropertyPartitionAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return FindPartitionInternalAsync(key, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<long> GetPropertyPartitionCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return GetPartitionCountInternalAsync(propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), logicalKey, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IPartition?> GetPropertyPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return GetDataPartitionByIndexInternalAsync(logicalKey, index, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<HeaderPartition?> GetHeaderPartitionAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        var result = await FindPartitionInternalAsync(new CompositePartitionKey(logicalKey, null), HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken);
        return result is HeaderPartition hp ? hp : null;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IPartition> GetAllHeaderPartitionsAsync(CancellationToken cancellationToken = default) 
        => GetAllPartitionsInternalAsync(HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, null, cancellationToken);

    #endregion

    #region Internal B+ Tree Logic

    private async Task InitializeInternalAsync(string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(treeMetrics.InitializationDuration);
        var indexStream = await getIndexStream(cancellationToken);
        
        if (indexStream.Length > 0)
        {
            await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken);
        }
        else
        {
            var header = new BTreeHeader(NextAvailableOffset: HeaderSize, PartitionCount: 0);
            indexStream.SetLength(HeaderSize);

            var root = new BPlusTreeNode { IsLeaf = true };
            var (rootOffset, newHeader) = await AllocateAndWriteNodeAsync(indexStream, header, root, propertyName, -1, cancellationToken);
        
            header = newHeader with { RootNodeOffset = rootOffset };
            await serializationService.WriteHeaderAsync(indexStream, header, HeaderSize, cancellationToken);
        }
    }
    
    private async Task<IPartition?> FindPartitionInternalAsync(CompositePartitionKey key, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(treeMetrics.FindDuration);
        var indexStream = await getIndexStream(cancellationToken);
        
        if (indexStream.Length <= HeaderSize) return null;
        
        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken);
        if (header.RootNodeOffset == -1) return null;
        
        return await FindInNodeAsync(indexStream, header.RootNodeOffset, key, propertyName, cancellationToken);
    }

    private async Task InsertPartitionInternalAsync(IPartition partition, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(treeMetrics.InsertDuration);
        var indexStream = await getIndexStream(cancellationToken);
        
        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken);
        if (header.RootNodeOffset == -1) throw new InvalidOperationException("Index strategy has not been initialized correctly, root node is missing.");

        var root = await ReadNodeAsync(indexStream, header.RootNodeOffset, propertyName, cancellationToken);
        if (root.Keys.Count == (2 * header.Degree - 1)) // Root is full
        {
            var oldRootOffset = header.RootNodeOffset;
            var newRoot = new BPlusTreeNode();
            newRoot.ChildrenOffsets.Add(oldRootOffset);

            header = await SplitChildAsync(indexStream, header, newRoot, 0, root, oldRootOffset, propertyName, cancellationToken);
            
            var (newRootOffset, newHeader) = await AllocateAndWriteNodeAsync(indexStream, header, newRoot, propertyName, -1, cancellationToken);
            header = newHeader;
            header = header with { RootNodeOffset = newRootOffset };
            
            var (finalRootOffset, finalHeader) = await InsertNonFullAsync(indexStream, header, newRoot, newRootOffset, partition, propertyName, cancellationToken);
            header = finalHeader with { RootNodeOffset = finalRootOffset };
        }
        else
        {
            var (newRootOffset, newHeader) = await InsertNonFullAsync(indexStream, header, root, header.RootNodeOffset, partition, propertyName, cancellationToken);
            header = newHeader with { RootNodeOffset = newRootOffset };
        }
        await serializationService.WriteHeaderAsync(indexStream, header, HeaderSize, cancellationToken);
    }

    private async Task UpdatePartitionInternalAsync(IPartition partition, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(treeMetrics.UpdateDuration);
        var indexStream = await getIndexStream(cancellationToken);
        
        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken);
        if (header.RootNodeOffset == -1) throw new InvalidOperationException("Index strategy has not been initialized correctly, root node is missing.");
        
        var (newRootOffset, newHeader) = await UpdateInNodeAsync(indexStream, header, header.RootNodeOffset, partition, propertyName, cancellationToken);
        var headerToWrite = newHeader with { RootNodeOffset = newRootOffset };
        await serializationService.WriteHeaderAsync(indexStream, headerToWrite, HeaderSize, cancellationToken);
    }

    private async Task DeletePartitionInternalAsync(IPartition partition, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(treeMetrics.DeleteDuration);
        var indexStream = await getIndexStream(cancellationToken);

        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken);
        if (header.RootNodeOffset == -1) throw new InvalidOperationException("Cannot delete from an empty tree.");
        
        var (newRootOffset, finalHeader) = await DeleteRecursiveAsync(indexStream, header, header.RootNodeOffset, partition, propertyName, cancellationToken);

        var root = await ReadNodeAsync(indexStream, newRootOffset, propertyName, cancellationToken);

        // If root becomes an internal node with no keys and one child, the child becomes the new root.
        if (!root.IsLeaf && root.Keys.Count == 0 && root.ChildrenOffsets.Count == 1)
        {
            long obsoleteRootOffset = newRootOffset;
            newRootOffset = root.ChildrenOffsets[0];
            finalHeader = await FreeNodeAsync(indexStream, finalHeader, obsoleteRootOffset, cancellationToken);
            RemoveFromCache((propertyName, obsoleteRootOffset));
        }

        var headerToWrite = finalHeader with { RootNodeOffset = newRootOffset };
        await serializationService.WriteHeaderAsync(indexStream, headerToWrite, HeaderSize, cancellationToken);
    }
    
    private async IAsyncEnumerable<IPartition> GetAllPartitionsInternalAsync(string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, IComparable? logicalKey = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = new MetricTimer(treeMetrics.GetAllDuration);
        var indexStream = await getIndexStream(cancellationToken);

        if (indexStream.Length <= HeaderSize) yield break;

        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken);
        if (header.RootNodeOffset == -1) yield break;
        
        await foreach (var partition in TraverseAndYieldPartitionsAsync(indexStream, header.RootNodeOffset, propertyName, logicalKey, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return partition;
        }
    }

    private async Task<long> GetPartitionCountInternalAsync(string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, IComparable? logicalKey = null, CancellationToken cancellationToken = default)
    {
        using var _ = new MetricTimer(treeMetrics.GetPartitionCountDuration);
        var indexStream = await getIndexStream(cancellationToken);
        if (indexStream.Length == 0) return 0;

        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken);
        if (logicalKey is null) return header.PartitionCount;

        long count = 0;
        await foreach (var partition in GetAllPartitionsInternalAsync(propertyName, getIndexStream, logicalKey, cancellationToken).WithCancellation(cancellationToken)) count++;
        return count;
    }

    private async Task<IPartition?> GetDataPartitionByIndexInternalAsync(IComparable logicalKey, long index, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(treeMetrics.GetDataPartitionByIndexDuration);
        if (index < 0) return null;

        long currentIndex = -1;
        await foreach (var partition in GetAllPartitionsInternalAsync(propertyName, getIndexStream, logicalKey, cancellationToken).WithCancellation(cancellationToken))
        {
            if (partition is DataPartition)
            {
                currentIndex++;
                if (currentIndex == index) return partition;
            }
        }
        return null;
    }

    private async IAsyncEnumerable<IPartition> TraverseAndYieldPartitionsAsync(Stream indexStream, long nodeOffset, string propertyName, IComparable? logicalKey, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (nodeOffset == -1) yield break;

        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken);

        if (node.IsLeaf)
        {
            foreach (var partition in node.Partitions)
            {
                if (logicalKey == null)
                {
                    yield return partition;
                }
                else
                {
                    var partitionKey = partition.GetPartitionKey();
                    int cmp = partitionKey.LogicalKey.CompareTo(logicalKey);

                    if (cmp == 0)
                    {
                        yield return partition;
                    }
                    else if (cmp > 0)
                    {
                        // Since the B+ Tree leaves are sorted by CompositePartitionKey,
                        // if we exceed the target logicalKey we can safely stop traversing this and subsequent nodes.
                        yield break;
                    }
                }
            }
        }
        else // Internal node
        {
            for (int i = 0; i < node.ChildrenOffsets.Count; i++)
            {
                bool shouldTraverse = true;
                
                if (logicalKey != null)
                {
                    // Internal nodes hold separation keys (CompositePartitionKey)
                    // Child i holds keys in range [node.Keys[i-1], node.Keys[i])
                    
                    // Prune upper bound
                    if (i < node.Keys.Count && node.Keys[i] is CompositePartitionKey upperBound)
                    {
                        if (upperBound.LogicalKey.CompareTo(logicalKey) < 0)
                        {
                            shouldTraverse = false;
                        }
                    }
                    
                    // Prune lower bound
                    if (i > 0 && node.Keys[i - 1] is CompositePartitionKey lowerBound)
                    {
                        if (lowerBound.LogicalKey.CompareTo(logicalKey) > 0)
                        {
                            shouldTraverse = false;
                        }
                    }
                }

                if (shouldTraverse)
                {
                    await foreach (var partition in TraverseAndYieldPartitionsAsync(indexStream, node.ChildrenOffsets[i], propertyName, logicalKey, cancellationToken).WithCancellation(cancellationToken))
                    {
                        yield return partition;
                    }
                }
            }
        }
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> DeleteRecursiveAsync(Stream indexStream, BTreeHeader header, long nodeOffset, IPartition partitionToDelete, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken);
        int t = header.Degree;
        bool nodeModified = false;
        var currentHeader = header;
        var key = partitionToDelete.GetPartitionKey();

        if (node.IsLeaf)
        {
            bool isDeletingHeader = partitionToDelete is HeaderPartition;
            int keyIndex = node.Partitions.FindIndex(p => p.GetPartitionKey().CompareTo(key) == 0 && p is HeaderPartition == isDeletingHeader);
            
            if (keyIndex == -1) throw new KeyNotFoundException($"Could not find a partition with key '{key}' to delete.");

            node.Keys.RemoveAt(keyIndex);
            node.Partitions.RemoveAt(keyIndex);
            currentHeader = currentHeader with { PartitionCount = currentHeader.PartitionCount - 1 };
            (nodeOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken);
            return (nodeOffset, currentHeader);
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0) childIndex++;

        var childOffset = node.ChildrenOffsets[childIndex];
        var childNode = await ReadNodeAsync(indexStream, childOffset, propertyName, cancellationToken);
        
        if (childNode.Keys.Count < t)
        {
            if (childIndex > 0)
            {
                var leftSiblingOffset = node.ChildrenOffsets[childIndex - 1];
                var leftSibling = await ReadNodeAsync(indexStream, leftSiblingOffset, propertyName, cancellationToken);
                if (leftSibling.Keys.Count >= t)
                {
                    (var h1, leftSiblingOffset, childOffset) = await BorrowFromSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, leftSibling, leftSiblingOffset, isLeftSibling: true, propertyName, cancellationToken);
                    currentHeader = h1;
                    node.ChildrenOffsets[childIndex - 1] = leftSiblingOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else
                {
                    (var h1, var mergedOffset, var mergedNode) = await MergeWithSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, leftSibling, leftSiblingOffset, isLeftSibling: true, propertyName, cancellationToken);
                    currentHeader = h1;
                    childIndex--;
                    childOffset = mergedOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
            else if (childIndex + 1 < node.ChildrenOffsets.Count)
            {
                var rightSiblingOffset = node.ChildrenOffsets[childIndex + 1];
                var rightSibling = await ReadNodeAsync(indexStream, rightSiblingOffset, propertyName, cancellationToken);
                if (rightSibling.Keys.Count >= t)
                {
                    (var h1, rightSiblingOffset, childOffset) = await BorrowFromSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, rightSibling, rightSiblingOffset, isLeftSibling: false, propertyName, cancellationToken);
                    currentHeader = h1;
                    node.ChildrenOffsets[childIndex + 1] = rightSiblingOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else
                {
                    (var h1, var mergedOffset, var mergedNode) = await MergeWithSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, rightSibling, rightSiblingOffset, isLeftSibling: false, propertyName, cancellationToken);
                    currentHeader = h1;
                    childOffset = mergedOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
        }

        var (newChildOffset, newChildHeader) = await DeleteRecursiveAsync(indexStream, currentHeader, childOffset, partitionToDelete, propertyName, cancellationToken);
        currentHeader = newChildHeader;
        if (newChildOffset != childOffset)
        {
            node.ChildrenOffsets[childIndex] = newChildOffset;
            nodeModified = true;
        }
        
        if (childIndex > 0)
        {
            var firstKeyInChildSubtree = await GetFirstKeyOfSubtree(indexStream, node.ChildrenOffsets[childIndex], propertyName, cancellationToken);
            if (firstKeyInChildSubtree != null && node.Keys[childIndex - 1].CompareTo(firstKeyInChildSubtree) != 0)
            {
                node.Keys[childIndex - 1] = firstKeyInChildSubtree;
                nodeModified = true;
            }
        }

        if (nodeModified)
        {
            (nodeOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken);
            return (nodeOffset, currentHeader);
        }

        return (nodeOffset, currentHeader);
    }
    
    private async Task<(BTreeHeader newHeader, long newSiblingOffset, long newChildOffset)> BorrowFromSiblingAsync(Stream stream, BTreeHeader header, BPlusTreeNode parent, int childIndex, BPlusTreeNode child, long childOffset, BPlusTreeNode sibling, long siblingOffset, bool isLeftSibling, string propertyName, CancellationToken cancellationToken)
    {
        treeMetrics.NodesBorrowed.Add(1);
        var currentHeader = header;
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
        
        (var newSiblingOffset, var h1) = await AllocateAndWriteNodeAsync(stream, currentHeader, sibling, propertyName, siblingOffset, cancellationToken);
        (var newChildOffset, var h2) = await AllocateAndWriteNodeAsync(stream, h1, child, propertyName, childOffset, cancellationToken);
        return (h2, newSiblingOffset, newChildOffset);
    }

    private async Task<(BTreeHeader newHeader, long mergedNodeOffset, BPlusTreeNode mergedNode)> MergeWithSiblingAsync(
        Stream stream, BTreeHeader header, BPlusTreeNode parent, int childIndex, 
        BPlusTreeNode child, long childOffset, BPlusTreeNode sibling, long siblingOffset, bool isLeftSibling, string propertyName, CancellationToken cancellationToken)
    {
        treeMetrics.NodesMerged.Add(1);
        BPlusTreeNode mergedNode;
        var currentHeader = header;
        long offsetToKeep, offsetToFree;

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
            mergedNode = sibling;
            offsetToKeep = siblingOffset;
            offsetToFree = childOffset;
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
            mergedNode = child;
            offsetToKeep = childOffset;
            offsetToFree = siblingOffset;
        }

        currentHeader = await FreeNodeAsync(stream, currentHeader, offsetToFree, cancellationToken);
        RemoveFromCache((propertyName, offsetToFree));

        var (offset, newHeader) = await AllocateAndWriteNodeAsync(stream, currentHeader, mergedNode, propertyName, offsetToKeep, cancellationToken);
        return (newHeader, offset, mergedNode);
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> InsertNonFullAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode node, long nodeOffset, IPartition partition, string propertyName, CancellationToken cancellationToken)
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
            (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken);
            return (newOffset, currentHeader);
        }
        else
        {
            int i = 0;
            while (i < node.Keys.Count && key.CompareTo(node.Keys[i]) > 0) i++;
            
            var childOffset = node.ChildrenOffsets[i];
            var childNode = await ReadNodeAsync(indexStream, childOffset, propertyName, cancellationToken);
            bool parentNeedsReallocation = false;

            if (childNode.Keys.Count == (2 * currentHeader.Degree - 1))
            {
                currentHeader = await SplitChildAsync(indexStream, currentHeader, node, i, childNode, childOffset, propertyName, cancellationToken);
                parentNeedsReallocation = true;

                if (key.CompareTo(node.Keys[i]) > 0) i++;
            }
            
            var childToInsertInOffset = node.ChildrenOffsets[i];
            var childToInsertIn = await ReadNodeAsync(indexStream, childToInsertInOffset, propertyName, cancellationToken);
            var (newChildOffset, newChildHeader) = await InsertNonFullAsync(indexStream, currentHeader, childToInsertIn, childToInsertInOffset, partition, propertyName, cancellationToken);
            currentHeader = newChildHeader;

            if (newChildOffset != childToInsertInOffset)
            {
                node.ChildrenOffsets[i] = newChildOffset;
                parentNeedsReallocation = true;
            }

            if (parentNeedsReallocation)
            {
                (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken);
                return (newOffset, currentHeader);
            }
            
            return (nodeOffset, currentHeader);
        }
    }

    private async Task<BTreeHeader> SplitChildAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode parentNode, int childIndex, BPlusTreeNode fullChildNode, long fullChildNodeOffset, string propertyName, CancellationToken cancellationToken)
    {
        treeMetrics.NodesSplit.Add(1);
        int t = header.Degree;
        var currentHeader = header;
        
        var rightNode = new BPlusTreeNode { IsLeaf = fullChildNode.IsLeaf };
        IComparable keyToPromote;
        
        int medianIndex = t - 1;

        if (fullChildNode.IsLeaf)
        {
            rightNode.Keys.AddRange(fullChildNode.Keys.Skip(t));
            rightNode.Partitions.AddRange(fullChildNode.Partitions.Skip(t));
            fullChildNode.Keys.RemoveRange(t, fullChildNode.Keys.Count - t);
            fullChildNode.Partitions.RemoveRange(t, fullChildNode.Partitions.Count - t);
            keyToPromote = rightNode.Keys[0];
        }
        else
        {
            keyToPromote = fullChildNode.Keys[medianIndex];
            rightNode.Keys.AddRange(fullChildNode.Keys.Skip(medianIndex + 1));
            rightNode.ChildrenOffsets.AddRange(fullChildNode.ChildrenOffsets.Skip(medianIndex + 1));
            fullChildNode.Keys.RemoveRange(medianIndex, fullChildNode.Keys.Count - medianIndex);
            fullChildNode.ChildrenOffsets.RemoveRange(medianIndex + 1, fullChildNode.ChildrenOffsets.Count - (medianIndex + 1));
        }

        var (rightNodeOffset, h1) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, rightNode, propertyName, -1, cancellationToken);
        currentHeader = h1;

        var (leftNodeOffset, h2) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, fullChildNode, propertyName, fullChildNodeOffset, cancellationToken);
        currentHeader = h2;

        parentNode.Keys.Insert(childIndex, keyToPromote);
        parentNode.ChildrenOffsets[childIndex] = leftNodeOffset;
        parentNode.ChildrenOffsets.Insert(childIndex + 1, rightNodeOffset);
        
        return currentHeader;
    }

    private async Task<IPartition?> FindInNodeAsync(Stream indexStream, long nodeOffset, CompositePartitionKey key, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken);

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

            if (candidatePartition is not null && candidatePartition.GetPartitionKey().LogicalKey.CompareTo(key.LogicalKey) == 0)
            {
                if (key.RangeKey is null && candidatePartition is not HeaderPartition) return null;
                return candidatePartition;
            }
            return null;
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0) childIndex++;
        
        return await FindInNodeAsync(indexStream, node.ChildrenOffsets[childIndex], key, propertyName, cancellationToken);
    }

    private async Task<(long newOffset, BTreeHeader newHeader)> UpdateInNodeAsync(Stream indexStream, BTreeHeader header, long nodeOffset, IPartition partition, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken);
        var key = partition.GetPartitionKey();
        var currentHeader = header;

        if (node.IsLeaf)
        {
            bool isUpdatingHeader = partition is HeaderPartition;
            int indexToUpdate = node.Partitions.FindIndex(p => p.GetPartitionKey().CompareTo(key) == 0 && p is HeaderPartition == isUpdatingHeader);

            if (indexToUpdate != -1)
            {
                node.Partitions[indexToUpdate] = partition;
                (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken);
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
            while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0) childIndex++;

            long childOffset = node.ChildrenOffsets[childIndex];
            var (newChildOffset, newChildHeader) = await UpdateInNodeAsync(indexStream, currentHeader, childOffset, partition, propertyName, cancellationToken);
            currentHeader = newChildHeader;

            if (newChildOffset != childOffset)
            {
                node.ChildrenOffsets[childIndex] = newChildOffset;
                (var newOffset, currentHeader) = await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken);
                return (newOffset, currentHeader);
            }
            
            return (nodeOffset, currentHeader);
        }
    }

    private async Task<(long offset, BTreeHeader newHeader)> AllocateAndWriteNodeAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode node, string propertyName, long oldOffset, CancellationToken cancellationToken)
    {
        treeMetrics.NodeWrites.Add(1);
        var nodeData = await serializationService.SerializeNodeToBytesAsync(node, cancellationToken);
        long requiredSize = nodeData.Length;
        
        long oldSize = -1;
        if (oldOffset != -1)
        {
            indexStream.Seek(oldOffset, SeekOrigin.Begin);
            var lengthBuffer = new byte[sizeof(int)];
            await indexStream.ReadExactlyAsync(lengthBuffer, cancellationToken);
            oldSize = BitConverter.ToInt32(lengthBuffer) + 4; // Include length prefix size
        }

        var freeState = new FreeSpaceState(header.NextAvailableOffset, header.FreeBlocks);
        var (offsetToUse, newState) = StreamSpaceAllocator.Allocate(freeState, requiredSize, oldOffset, oldSize);

        if (oldOffset != -1)
        {
            if (offsetToUse == oldOffset)
            {
                treeMetrics.InPlaceOverwrites.Add(1);
                treeMetrics.BytesSaved.Add(requiredSize);
            }
            else
            {
                treeMetrics.BlocksFreed.Add(1);
            }
        }

        if (offsetToUse != oldOffset && newState.NextAvailableOffset == freeState.NextAvailableOffset)
        {
            treeMetrics.BlocksReused.Add(1);
            treeMetrics.BytesSaved.Add(requiredSize);
        }

        var currentHeader = header with { NextAvailableOffset = newState.NextAvailableOffset, FreeBlocks = newState.FreeBlocks?.ToList() };

        await serializationService.WriteNodeBytesAsync(indexStream, nodeData, offsetToUse, cancellationToken);
        
        AddToCache((propertyName, offsetToUse), node);
        if (oldOffset != -1 && oldOffset != offsetToUse)
        {
            RemoveFromCache((propertyName, oldOffset));
        }
        
        return (offsetToUse, currentHeader);
    }
    
    private async Task<BTreeHeader> FreeNodeAsync(Stream stream, BTreeHeader header, long offset, CancellationToken cancellationToken)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        var lengthBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
        long size = BitConverter.ToInt32(lengthBuffer) + 4;

        var freeState = new FreeSpaceState(header.NextAvailableOffset, header.FreeBlocks);
        var newState = StreamSpaceAllocator.Free(freeState, offset, size);
        treeMetrics.BlocksFreed.Add(1);

        return header with { FreeBlocks = newState.FreeBlocks?.ToList() };
    }

    private async Task<IComparable?> GetFirstKeyOfSubtree(Stream stream, long nodeOffset, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(stream, nodeOffset, propertyName, cancellationToken);
        if (node.IsLeaf) return node.Keys.Count > 0 ? node.Keys[0] : null;
        
        return node.ChildrenOffsets.Count > 0 
            ? await GetFirstKeyOfSubtree(stream, node.ChildrenOffsets[0], propertyName, cancellationToken) 
            : null;
    }
    
    private void RemoveFromCache((string propertyName, long offset) key)
    {
        if (nodeCache.TryGetValue(key, out var existingNode))
        {
            lruList.Remove(existingNode);
            nodeCache.Remove(key);
        }
    }

    private void AddToCache((string propertyName, long offset) key, BPlusTreeNode node)
    {
        if (nodeCache.ContainsKey(key)) RemoveFromCache(key);

        if (nodeCache.Count >= MaxCacheSize)
        {
            var lru = lruList.Last;
            if (lru != null)
            {
                lruList.RemoveLast();
                nodeCache.Remove(lru.Value.Key);
            }
        }
    
        var newNode = new LinkedListNode<KeyValuePair<(string, long), BPlusTreeNode>>(new KeyValuePair<(string, long), BPlusTreeNode>(key, node));
        lruList.AddFirst(newNode);
        nodeCache[key] = newNode;
    }

    private async Task<BPlusTreeNode> ReadNodeAsync(Stream indexStream, long nodeOffset, string propertyName, CancellationToken cancellationToken)
    {
        if (nodeCache.TryGetValue((propertyName, nodeOffset), out var linkedListNode))
        {
            lruList.Remove(linkedListNode);
            lruList.AddFirst(linkedListNode);
            return linkedListNode.Value.Value;
        }

        treeMetrics.NodeReads.Add(1);
        var node = await serializationService.ReadNodeAsync(indexStream, nodeOffset, cancellationToken);
        AddToCache((propertyName, nodeOffset), node);
        return node;
    }

    #endregion
}