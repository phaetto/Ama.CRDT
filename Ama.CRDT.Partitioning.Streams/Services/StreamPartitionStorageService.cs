namespace Ama.CRDT.Partitioning.Streams.Services;

using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Partitioning.Streams.Services.Metrics;
using Ama.CRDT.Partitioning.Streams.Services.Serialization;
using Ama.CRDT.Services.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
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

    // Concurrency controls
    private readonly ConcurrentDictionary<string, AsyncLock> locks = new();
    private readonly object cacheLock = new();

    // Cache for B+ Tree nodes, keyed by a distinct structure to support multiple index streams.
    private readonly Dictionary<CacheKey, LinkedListNode<KeyValuePair<CacheKey, BPlusTreeNode>>> nodeCache = new();
    private readonly LinkedList<KeyValuePair<CacheKey, BPlusTreeNode>> lruList = new();

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
    public async Task<CrdtDocument<TData>> LoadPartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partition, CancellationToken cancellationToken = default) where TData : class
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);

        var streamLock = GetLock(GetDataLockKey(logicalKey, propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new MetricTimer(metrics.StreamReadDuration);
        var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
        return await LoadContentInternalAsync<TData>(partition, dataStream, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<TData>> LoadHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partition, CancellationToken cancellationToken = default) where TData : class
    {
        ArgumentNullException.ThrowIfNull(logicalKey);

        var streamLock = GetLock(GetDataLockKey(logicalKey, HeaderIdentifier));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new MetricTimer(metrics.StreamReadDuration);
        var dataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        return await LoadContentInternalAsync<TData>(partition, dataStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CrdtDocument<TData>> LoadContentInternalAsync<TData>(IPartition partition, Stream dataStream, CancellationToken cancellationToken) where TData : class
    {
        var docBuffer = new byte[partition.DataLength];
        dataStream.Seek(partition.DataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(docBuffer, cancellationToken).ConfigureAwait(false);
        using var docStream = new MemoryStream(docBuffer);
        var doc = await serializationService.DeserializeObjectAsync<TData>(docStream, cancellationToken).ConfigureAwait(false);
        
        var metaBuffer = new byte[partition.MetadataLength];
        dataStream.Seek(partition.MetadataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(metaBuffer, cancellationToken).ConfigureAwait(false);
        using var metaStream = new MemoryStream(metaBuffer);
        var meta = await serializationService.DeserializeObjectAsync<CrdtMetadata>(metaStream, cancellationToken).ConfigureAwait(false);

        return new CrdtDocument<TData>(doc!, meta!);
    }

    /// <inheritdoc/>
    public async Task<IPartition> SavePartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partitionToUpdate);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metadata);

        var streamLock = GetLock(GetDataLockKey(logicalKey, propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new MetricTimer(metrics.StreamWriteDuration);
        var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
        var header = await ReadOrCreateDataHeaderAsync(dataStream, cancellationToken).ConfigureAwait(false);
        
        long oldDataOffset = partitionToUpdate is DataPartition dp1 && dp1.DataOffset > 0 ? dp1.DataOffset : -1;
        long oldDataLength = partitionToUpdate.DataLength;
        long oldMetaOffset = partitionToUpdate is DataPartition dp2 && dp2.MetadataOffset > 0 ? dp2.MetadataOffset : -1;
        long oldMetaLength = partitionToUpdate.MetadataLength;

        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, header, oldDataOffset, oldDataLength, cancellationToken).ConfigureAwait(false);
        header = newDataWriteResult.Header;

        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, header, oldMetaOffset, oldMetaLength, cancellationToken).ConfigureAwait(false);
        header = newMetaWriteResult.Header;

        await WriteDataHeaderAsync(dataStream, header, cancellationToken).ConfigureAwait(false);

        return partitionToUpdate switch
        {
            DataPartition dp => dp with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length },
            _ => throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}")
        };
    }

    /// <inheritdoc/>
    public async Task<HeaderPartition> SaveHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metadata);

        var streamLock = GetLock(GetDataLockKey(logicalKey, HeaderIdentifier));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new MetricTimer(metrics.StreamWriteDuration);
        var dataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        var header = await ReadOrCreateDataHeaderAsync(dataStream, cancellationToken).ConfigureAwait(false);
        
        long oldDataOffset = partitionToUpdate.DataOffset > 0 ? partitionToUpdate.DataOffset : -1;
        long oldDataLength = partitionToUpdate.DataLength;
        long oldMetaOffset = partitionToUpdate.MetadataOffset > 0 ? partitionToUpdate.MetadataOffset : -1;
        long oldMetaLength = partitionToUpdate.MetadataLength;

        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, header, oldDataOffset, oldDataLength, cancellationToken).ConfigureAwait(false);
        header = newDataWriteResult.Header;

        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, header, oldMetaOffset, oldMetaLength, cancellationToken).ConfigureAwait(false);
        header = newMetaWriteResult.Header;

        await WriteDataHeaderAsync(dataStream, header, cancellationToken).ConfigureAwait(false);

        return partitionToUpdate with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length };
    }

    private async Task<StreamWriteResult> WriteToStreamAsync(
        Stream dataStream, object content, DataStreamHeader header, long oldOffset, long oldLength, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await serializationService.SerializeObjectAsync(ms, content, cancellationToken).ConfigureAwait(false);
        var requiredSize = ms.Length;

        var freeState = new FreeSpaceState(header.NextAvailableOffset, header.FreeBlocks);
        
        // Force allocation to new space for crash resilience (Copy-on-Write)
        var (offsetToUse, newState) = StreamSpaceAllocator.Allocate(freeState, requiredSize, -1, -1);

        dataStream.Seek(offsetToUse, SeekOrigin.Begin);
        ms.Seek(0, SeekOrigin.Begin);
        await ms.CopyToAsync(dataStream, cancellationToken).ConfigureAwait(false);
        await dataStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Safe to logically free the old space now. Will be committed when header is written.
        if (oldOffset != -1)
        {
            newState = StreamSpaceAllocator.Free(newState, oldOffset, oldLength);
        }

        var newHeader = header with { NextAvailableOffset = newState.NextAvailableOffset, FreeBlocks = newState.FreeBlocks };
        return new StreamWriteResult(offsetToUse, requiredSize, newHeader);
    }

    private async Task<DataStreamHeader> ReadOrCreateDataHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length < HeaderSize)
        {
            var newHeader = new DataStreamHeader();
            await WriteDataHeaderAsync(stream, newHeader, cancellationToken).ConfigureAwait(false);
            return newHeader;
        }

        var buffer = new byte[HeaderSize];
        stream.Seek(0, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        var jsonString = Encoding.UTF8.GetString(buffer).TrimEnd();
        
        if (string.IsNullOrWhiteSpace(jsonString)) 
        {
            var newHeader = new DataStreamHeader();
            await WriteDataHeaderAsync(stream, newHeader, cancellationToken).ConfigureAwait(false);
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

        // Ensure payload flushes are on disk before updating the pointer.
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        stream.Seek(0, SeekOrigin.Begin);
        await stream.WriteAsync(padded, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearPropertyDataAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var streamLock = GetLock(GetDataLockKey(logicalKey, propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        var stream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
        stream.SetLength(0);
        await ReadOrCreateDataHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearHeaderDataAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);

        var streamLock = GetLock(GetDataLockKey(logicalKey, HeaderIdentifier));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        var stream = await streamProvider.GetHeaderDataStreamAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        stream.SetLength(0);
        await ReadOrCreateDataHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Index Stream Operations (Formerly IPartitioningStrategy)

    /// <inheritdoc/>
    public async Task InitializePropertyIndexAsync(string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await InitializeInternalAsync(propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task InitializeHeaderIndexAsync(CancellationToken cancellationToken = default)
    {
        var streamLock = GetLock(GetIndexLockKey(HeaderIdentifier));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await InitializeInternalAsync(HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task InsertPropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);

        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await InsertPartitionInternalAsync(partition, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task InsertHeaderPartitionAsync(IComparable logicalKey, HeaderPartition headerPartition, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);

        var streamLock = GetLock(GetIndexLockKey(HeaderIdentifier));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await InsertPartitionInternalAsync(headerPartition, HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdatePropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);

        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await UpdatePartitionInternalAsync(partition, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateHeaderPartitionAsync(IComparable logicalKey, HeaderPartition headerPartition, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);

        var streamLock = GetLock(GetIndexLockKey(HeaderIdentifier));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await UpdatePartitionInternalAsync(headerPartition, HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeletePropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(partition);

        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await DeletePartitionInternalAsync(partition, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IPartition> GetPartitionsAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return GetAllPartitionsInternalAsync(propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), logicalKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetPropertyPartitionAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        return await FindPartitionInternalAsync(key, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> GetPropertyPartitionCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        return await GetPartitionCountInternalAsync(propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), logicalKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetPropertyPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        return await GetDataPartitionByIndexInternalAsync(logicalKey, index, propertyName, ct => streamProvider.GetPropertyIndexStreamAsync(propertyName, ct), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<HeaderPartition?> GetHeaderPartitionAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);

        var streamLock = GetLock(GetIndexLockKey(HeaderIdentifier));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        var result = await FindPartitionInternalAsync(new CompositePartitionKey(logicalKey, null), HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, cancellationToken).ConfigureAwait(false);
        return result is HeaderPartition hp ? hp : null;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IPartition> GetAllHeaderPartitionsAsync(CancellationToken cancellationToken = default) 
        => GetAllPartitionsInternalAsync(HeaderIdentifier, streamProvider.GetHeaderIndexStreamAsync, null, cancellationToken);

    #endregion

    #region Internal B+ Tree Logic

    private async Task InitializeInternalAsync(string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var timer = new MetricTimer(treeMetrics.InitializationDuration);
        var indexStream = await getIndexStream(cancellationToken).ConfigureAwait(false);
        
        if (indexStream.Length > 0)
        {
            await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var header = new BTreeHeader(NextAvailableOffset: HeaderSize, PartitionCount: 0);
            indexStream.SetLength(HeaderSize);

            var root = new BPlusTreeNode { IsLeaf = true };
            var rootWriteResult = await AllocateAndWriteNodeAsync(indexStream, header, root, propertyName, -1, cancellationToken).ConfigureAwait(false);
        
            header = rootWriteResult.Header with { RootNodeOffset = rootWriteResult.Offset };
            await serializationService.WriteHeaderAsync(indexStream, header, HeaderSize, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async Task<IPartition?> FindPartitionInternalAsync(CompositePartitionKey key, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var timer = new MetricTimer(treeMetrics.FindDuration);
        var indexStream = await getIndexStream(cancellationToken).ConfigureAwait(false);
        
        if (indexStream.Length <= HeaderSize) return null;
        
        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken).ConfigureAwait(false);
        if (header.RootNodeOffset == -1) return null;
        
        return await FindInNodeAsync(indexStream, header.RootNodeOffset, key, propertyName, cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertPartitionInternalAsync(IPartition partition, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var timer = new MetricTimer(treeMetrics.InsertDuration);
        var indexStream = await getIndexStream(cancellationToken).ConfigureAwait(false);
        
        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken).ConfigureAwait(false);
        if (header.RootNodeOffset == -1) throw new InvalidOperationException("Index strategy has not been initialized correctly, root node is missing.");

        var root = await ReadNodeAsync(indexStream, header.RootNodeOffset, propertyName, cancellationToken).ConfigureAwait(false);
        if (root.Keys.Count == (2 * header.Degree - 1)) // Root is full
        {
            var oldRootOffset = header.RootNodeOffset;
            var newRoot = new BPlusTreeNode();
            newRoot.ChildrenOffsets.Add(oldRootOffset);

            header = await SplitChildAsync(indexStream, header, newRoot, 0, root, oldRootOffset, propertyName, cancellationToken).ConfigureAwait(false);
            
            var newRootWriteResult = await AllocateAndWriteNodeAsync(indexStream, header, newRoot, propertyName, -1, cancellationToken).ConfigureAwait(false);
            header = newRootWriteResult.Header;
            header = header with { RootNodeOffset = newRootWriteResult.Offset };
            
            var finalRootWriteResult = await InsertNonFullAsync(indexStream, header, newRoot, newRootWriteResult.Offset, partition, propertyName, cancellationToken).ConfigureAwait(false);
            header = finalRootWriteResult.Header with { RootNodeOffset = finalRootWriteResult.Offset };
        }
        else
        {
            var newRootWriteResult = await InsertNonFullAsync(indexStream, header, root, header.RootNodeOffset, partition, propertyName, cancellationToken).ConfigureAwait(false);
            header = newRootWriteResult.Header with { RootNodeOffset = newRootWriteResult.Offset };
        }
        await serializationService.WriteHeaderAsync(indexStream, header, HeaderSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdatePartitionInternalAsync(IPartition partition, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var timer = new MetricTimer(treeMetrics.UpdateDuration);
        var indexStream = await getIndexStream(cancellationToken).ConfigureAwait(false);
        
        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken).ConfigureAwait(false);
        if (header.RootNodeOffset == -1) throw new InvalidOperationException("Index strategy has not been initialized correctly, root node is missing.");
        
        var rootWriteResult = await UpdateInNodeAsync(indexStream, header, header.RootNodeOffset, partition, propertyName, cancellationToken).ConfigureAwait(false);
        var headerToWrite = rootWriteResult.Header with { RootNodeOffset = rootWriteResult.Offset };
        await serializationService.WriteHeaderAsync(indexStream, headerToWrite, HeaderSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeletePartitionInternalAsync(IPartition partition, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var timer = new MetricTimer(treeMetrics.DeleteDuration);
        var indexStream = await getIndexStream(cancellationToken).ConfigureAwait(false);

        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken).ConfigureAwait(false);
        if (header.RootNodeOffset == -1) throw new InvalidOperationException("Cannot delete from an empty tree.");
        
        var rootWriteResult = await DeleteRecursiveAsync(indexStream, header, header.RootNodeOffset, partition, propertyName, cancellationToken).ConfigureAwait(false);
        var newRootOffset = rootWriteResult.Offset;
        var finalHeader = rootWriteResult.Header;

        var root = await ReadNodeAsync(indexStream, newRootOffset, propertyName, cancellationToken).ConfigureAwait(false);

        // If root becomes an internal node with no keys and one child, the child becomes the new root.
        if (!root.IsLeaf && root.Keys.Count == 0 && root.ChildrenOffsets.Count == 1)
        {
            long obsoleteRootOffset = newRootOffset;
            newRootOffset = root.ChildrenOffsets[0];
            finalHeader = await FreeNodeAsync(indexStream, finalHeader, obsoleteRootOffset, cancellationToken).ConfigureAwait(false);
            RemoveFromCache(new CacheKey(propertyName, obsoleteRootOffset));
        }

        var headerToWrite = finalHeader with { RootNodeOffset = newRootOffset };
        await serializationService.WriteHeaderAsync(indexStream, headerToWrite, HeaderSize, cancellationToken).ConfigureAwait(false);
    }
    
    private async IAsyncEnumerable<IPartition> GetAllPartitionsInternalAsync(string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, IComparable? logicalKey = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamLock = GetLock(GetIndexLockKey(propertyName));
        using var releaser = await streamLock.LockAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var partition in GetAllPartitionsNoLockAsync(propertyName, getIndexStream, logicalKey, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return partition;
        }
    }

    private async IAsyncEnumerable<IPartition> GetAllPartitionsNoLockAsync(string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, IComparable? logicalKey = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timer = new MetricTimer(treeMetrics.GetAllDuration);
        var indexStream = await getIndexStream(cancellationToken).ConfigureAwait(false);

        if (indexStream.Length <= HeaderSize) yield break;

        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken).ConfigureAwait(false);
        if (header.RootNodeOffset == -1) yield break;
        
        await foreach (var partition in TraverseAndYieldPartitionsAsync(indexStream, header.RootNodeOffset, propertyName, logicalKey, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return partition;
        }
    }

    private async Task<long> GetPartitionCountInternalAsync(string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, IComparable? logicalKey = null, CancellationToken cancellationToken = default)
    {
        using var timer = new MetricTimer(treeMetrics.GetPartitionCountDuration);
        var indexStream = await getIndexStream(cancellationToken).ConfigureAwait(false);
        if (indexStream.Length == 0) return 0;

        var header = await serializationService.ReadHeaderAsync(indexStream, HeaderSize, cancellationToken).ConfigureAwait(false);
        if (logicalKey is null) return header.PartitionCount;

        long count = 0;
        await foreach (var partition in GetAllPartitionsNoLockAsync(propertyName, getIndexStream, logicalKey, cancellationToken).WithCancellation(cancellationToken)) count++;
        return count;
    }

    private async Task<IPartition?> GetDataPartitionByIndexInternalAsync(IComparable logicalKey, long index, string propertyName, Func<CancellationToken, Task<Stream>> getIndexStream, CancellationToken cancellationToken)
    {
        using var timer = new MetricTimer(treeMetrics.GetDataPartitionByIndexDuration);
        if (index < 0) return null;

        long currentIndex = -1;
        await foreach (var partition in GetAllPartitionsNoLockAsync(propertyName, getIndexStream, logicalKey, cancellationToken).WithCancellation(cancellationToken))
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

        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken).ConfigureAwait(false);

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

    private async Task<NodeWriteResult> DeleteRecursiveAsync(Stream indexStream, BTreeHeader header, long nodeOffset, IPartition partitionToDelete, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken).ConfigureAwait(false);
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
            return await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken).ConfigureAwait(false);
        }

        int childIndex = 0;
        while (childIndex < node.Keys.Count && key.CompareTo(node.Keys[childIndex]) >= 0) childIndex++;

        var childOffset = node.ChildrenOffsets[childIndex];
        var childNode = await ReadNodeAsync(indexStream, childOffset, propertyName, cancellationToken).ConfigureAwait(false);
        
        if (childNode.Keys.Count < t)
        {
            if (childIndex > 0)
            {
                var leftSiblingOffset = node.ChildrenOffsets[childIndex - 1];
                var leftSibling = await ReadNodeAsync(indexStream, leftSiblingOffset, propertyName, cancellationToken).ConfigureAwait(false);
                if (leftSibling.Keys.Count >= t)
                {
                    var borrowResult = await BorrowFromSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, leftSibling, leftSiblingOffset, isLeftSibling: true, propertyName, cancellationToken).ConfigureAwait(false);
                    currentHeader = borrowResult.Header;
                    node.ChildrenOffsets[childIndex - 1] = borrowResult.SiblingOffset;
                    childOffset = borrowResult.ChildOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else
                {
                    var mergeResult = await MergeWithSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, leftSibling, leftSiblingOffset, isLeftSibling: true, propertyName, cancellationToken).ConfigureAwait(false);
                    currentHeader = mergeResult.Header;
                    childIndex--;
                    childOffset = mergeResult.MergedOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
            else if (childIndex + 1 < node.ChildrenOffsets.Count)
            {
                var rightSiblingOffset = node.ChildrenOffsets[childIndex + 1];
                var rightSibling = await ReadNodeAsync(indexStream, rightSiblingOffset, propertyName, cancellationToken).ConfigureAwait(false);
                if (rightSibling.Keys.Count >= t)
                {
                    var borrowResult = await BorrowFromSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, rightSibling, rightSiblingOffset, isLeftSibling: false, propertyName, cancellationToken).ConfigureAwait(false);
                    currentHeader = borrowResult.Header;
                    node.ChildrenOffsets[childIndex + 1] = borrowResult.SiblingOffset;
                    childOffset = borrowResult.ChildOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                }
                else
                {
                    var mergeResult = await MergeWithSiblingAsync(indexStream, currentHeader, node, childIndex, childNode, childOffset, rightSibling, rightSiblingOffset, isLeftSibling: false, propertyName, cancellationToken).ConfigureAwait(false);
                    currentHeader = mergeResult.Header;
                    childOffset = mergeResult.MergedOffset;
                    node.ChildrenOffsets[childIndex] = childOffset;
                    node.ChildrenOffsets.RemoveAt(childIndex + 1);
                }
                nodeModified = true;
            }
        }

        var newChildWriteResult = await DeleteRecursiveAsync(indexStream, currentHeader, childOffset, partitionToDelete, propertyName, cancellationToken).ConfigureAwait(false);
        currentHeader = newChildWriteResult.Header;
        if (newChildWriteResult.Offset != childOffset)
        {
            node.ChildrenOffsets[childIndex] = newChildWriteResult.Offset;
            nodeModified = true;
        }
        
        if (childIndex > 0)
        {
            var firstKeyInChildSubtree = await GetFirstKeyOfSubtree(indexStream, node.ChildrenOffsets[childIndex], propertyName, cancellationToken).ConfigureAwait(false);
            if (firstKeyInChildSubtree != null && node.Keys[childIndex - 1].CompareTo(firstKeyInChildSubtree) != 0)
            {
                node.Keys[childIndex - 1] = firstKeyInChildSubtree;
                nodeModified = true;
            }
        }

        if (nodeModified)
        {
            return await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken).ConfigureAwait(false);
        }

        return new NodeWriteResult(nodeOffset, currentHeader);
    }
    
    private async Task<BorrowResult> BorrowFromSiblingAsync(Stream stream, BTreeHeader header, BPlusTreeNode parent, int childIndex, BPlusTreeNode child, long childOffset, BPlusTreeNode sibling, long siblingOffset, bool isLeftSibling, string propertyName, CancellationToken cancellationToken)
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
        
        var siblingWriteResult = await AllocateAndWriteNodeAsync(stream, currentHeader, sibling, propertyName, siblingOffset, cancellationToken).ConfigureAwait(false);
        var childWriteResult = await AllocateAndWriteNodeAsync(stream, siblingWriteResult.Header, child, propertyName, childOffset, cancellationToken).ConfigureAwait(false);
        return new BorrowResult(childWriteResult.Header, siblingWriteResult.Offset, childWriteResult.Offset);
    }

    private async Task<MergeResult> MergeWithSiblingAsync(
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

        currentHeader = await FreeNodeAsync(stream, currentHeader, offsetToFree, cancellationToken).ConfigureAwait(false);
        RemoveFromCache(new CacheKey(propertyName, offsetToFree));

        var mergedWriteResult = await AllocateAndWriteNodeAsync(stream, currentHeader, mergedNode, propertyName, offsetToKeep, cancellationToken).ConfigureAwait(false);
        return new MergeResult(mergedWriteResult.Header, mergedWriteResult.Offset, mergedNode);
    }

    private async Task<NodeWriteResult> InsertNonFullAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode node, long nodeOffset, IPartition partition, string propertyName, CancellationToken cancellationToken)
    {
        var key = partition.GetPartitionKey();
        var currentHeader = header;
        
        if (node.IsLeaf)
        {
            int i = 0;
            while (i < node.Keys.Count && key.CompareTo(node.Keys[i]) >= 0) i++;
            node.Keys.Insert(i, key);
            node.Partitions.Insert(i, partition);
            
            currentHeader = currentHeader with { PartitionCount = currentHeader.PartitionCount + 1 };
            return await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            int i = 0;
            while (i < node.Keys.Count && key.CompareTo(node.Keys[i]) >= 0) i++;
            
            var childOffset = node.ChildrenOffsets[i];
            var childNode = await ReadNodeAsync(indexStream, childOffset, propertyName, cancellationToken).ConfigureAwait(false);
            bool parentNeedsReallocation = false;

            if (childNode.Keys.Count == (2 * currentHeader.Degree - 1))
            {
                currentHeader = await SplitChildAsync(indexStream, currentHeader, node, i, childNode, childOffset, propertyName, cancellationToken).ConfigureAwait(false);
                parentNeedsReallocation = true;

                if (key.CompareTo(node.Keys[i]) >= 0) i++;
            }
            
            var childToInsertInOffset = node.ChildrenOffsets[i];
            var childToInsertIn = await ReadNodeAsync(indexStream, childToInsertInOffset, propertyName, cancellationToken).ConfigureAwait(false);
            var childWriteResult = await InsertNonFullAsync(indexStream, currentHeader, childToInsertIn, childToInsertInOffset, partition, propertyName, cancellationToken).ConfigureAwait(false);
            currentHeader = childWriteResult.Header;

            if (childWriteResult.Offset != childToInsertInOffset)
            {
                node.ChildrenOffsets[i] = childWriteResult.Offset;
                parentNeedsReallocation = true;
            }

            if (parentNeedsReallocation)
            {
                return await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken).ConfigureAwait(false);
            }
            
            return new NodeWriteResult(nodeOffset, currentHeader);
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

        var rightWriteResult = await AllocateAndWriteNodeAsync(indexStream, currentHeader, rightNode, propertyName, -1, cancellationToken).ConfigureAwait(false);
        currentHeader = rightWriteResult.Header;

        var leftWriteResult = await AllocateAndWriteNodeAsync(indexStream, currentHeader, fullChildNode, propertyName, fullChildNodeOffset, cancellationToken).ConfigureAwait(false);
        currentHeader = leftWriteResult.Header;

        parentNode.Keys.Insert(childIndex, keyToPromote);
        parentNode.ChildrenOffsets[childIndex] = leftWriteResult.Offset;
        parentNode.ChildrenOffsets.Insert(childIndex + 1, rightWriteResult.Offset);
        
        return currentHeader;
    }

    private async Task<IPartition?> FindInNodeAsync(Stream indexStream, long nodeOffset, CompositePartitionKey key, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken).ConfigureAwait(false);

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
        
        return await FindInNodeAsync(indexStream, node.ChildrenOffsets[childIndex], key, propertyName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeWriteResult> UpdateInNodeAsync(Stream indexStream, BTreeHeader header, long nodeOffset, IPartition partition, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(indexStream, nodeOffset, propertyName, cancellationToken).ConfigureAwait(false);
        var key = partition.GetPartitionKey();
        var currentHeader = header;

        if (node.IsLeaf)
        {
            bool isUpdatingHeader = partition is HeaderPartition;
            int indexToUpdate = node.Partitions.FindIndex(p => p.GetPartitionKey().CompareTo(key) == 0 && p is HeaderPartition == isUpdatingHeader);

            if (indexToUpdate != -1)
            {
                node.Partitions[indexToUpdate] = partition;
                return await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken).ConfigureAwait(false);
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
            var childWriteResult = await UpdateInNodeAsync(indexStream, currentHeader, childOffset, partition, propertyName, cancellationToken).ConfigureAwait(false);
            currentHeader = childWriteResult.Header;

            if (childWriteResult.Offset != childOffset)
            {
                node.ChildrenOffsets[childIndex] = childWriteResult.Offset;
                return await AllocateAndWriteNodeAsync(indexStream, currentHeader, node, propertyName, nodeOffset, cancellationToken).ConfigureAwait(false);
            }
            
            return new NodeWriteResult(nodeOffset, currentHeader);
        }
    }

    private async Task<NodeWriteResult> AllocateAndWriteNodeAsync(Stream indexStream, BTreeHeader header, BPlusTreeNode node, string propertyName, long oldOffset, CancellationToken cancellationToken)
    {
        treeMetrics.NodeWrites.Add(1);
        var nodeData = await serializationService.SerializeNodeToBytesAsync(node, cancellationToken).ConfigureAwait(false);
        long requiredSize = nodeData.Length;
        
        long oldSize = -1;
        if (oldOffset != -1)
        {
            indexStream.Seek(oldOffset, SeekOrigin.Begin);
            var lengthBuffer = new byte[sizeof(int)];
            await indexStream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
            oldSize = BitConverter.ToInt32(lengthBuffer) + 4; // Include length prefix size
        }

        var freeState = new FreeSpaceState(header.NextAvailableOffset, header.FreeBlocks);
        
        // Force new allocation for CoW (Crash Resilience)
        var (offsetToUse, newState) = StreamSpaceAllocator.Allocate(freeState, requiredSize, -1, -1);

        if (oldOffset != -1)
        {
            newState = StreamSpaceAllocator.Free(newState, oldOffset, oldSize);
            treeMetrics.BlocksFreed.Add(1);
        }

        var currentHeader = header with { NextAvailableOffset = newState.NextAvailableOffset, FreeBlocks = newState.FreeBlocks?.ToList() };

        await serializationService.WriteNodeBytesAsync(indexStream, nodeData, offsetToUse, cancellationToken).ConfigureAwait(false);

        // Ensure nodes are flushed safely before any header pointer updates.
        await indexStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        
        AddToCache(new CacheKey(propertyName, offsetToUse), node);
        if (oldOffset != -1 && oldOffset != offsetToUse)
        {
            RemoveFromCache(new CacheKey(propertyName, oldOffset));
        }
        
        return new NodeWriteResult(offsetToUse, currentHeader);
    }
    
    private async Task<BTreeHeader> FreeNodeAsync(Stream stream, BTreeHeader header, long offset, CancellationToken cancellationToken)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        var lengthBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
        long size = BitConverter.ToInt32(lengthBuffer) + 4;

        var freeState = new FreeSpaceState(header.NextAvailableOffset, header.FreeBlocks);
        var newState = StreamSpaceAllocator.Free(freeState, offset, size);
        treeMetrics.BlocksFreed.Add(1);

        return header with { FreeBlocks = newState.FreeBlocks?.ToList() };
    }

    private async Task<IComparable?> GetFirstKeyOfSubtree(Stream stream, long nodeOffset, string propertyName, CancellationToken cancellationToken)
    {
        var node = await ReadNodeAsync(stream, nodeOffset, propertyName, cancellationToken).ConfigureAwait(false);
        if (node.IsLeaf) return node.Keys.Count > 0 ? node.Keys[0] : null;
        
        return node.ChildrenOffsets.Count > 0 
            ? await GetFirstKeyOfSubtree(stream, node.ChildrenOffsets[0], propertyName, cancellationToken).ConfigureAwait(false)
            : null;
    }
    
    private void RemoveFromCache(CacheKey key)
    {
        lock (cacheLock)
        {
            if (nodeCache.TryGetValue(key, out var existingNode))
            {
                lruList.Remove(existingNode);
                nodeCache.Remove(key);
            }
        }
    }

    private void AddToCache(CacheKey key, BPlusTreeNode node)
    {
        lock (cacheLock)
        {
            if (nodeCache.TryGetValue(key, out var existingNode))
            {
                lruList.Remove(existingNode);
                nodeCache.Remove(key);
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
        
            var newNode = new LinkedListNode<KeyValuePair<CacheKey, BPlusTreeNode>>(new KeyValuePair<CacheKey, BPlusTreeNode>(key, node));
            lruList.AddFirst(newNode);
            nodeCache[key] = newNode;
        }
    }

    private async Task<BPlusTreeNode> ReadNodeAsync(Stream indexStream, long nodeOffset, string propertyName, CancellationToken cancellationToken)
    {
        lock (cacheLock)
        {
            if (nodeCache.TryGetValue(new CacheKey(propertyName, nodeOffset), out var linkedListNode))
            {
                lruList.Remove(linkedListNode);
                lruList.AddFirst(linkedListNode);
                return linkedListNode.Value.Value;
            }
        }

        treeMetrics.NodeReads.Add(1);
        var node = await serializationService.ReadNodeAsync(indexStream, nodeOffset, cancellationToken).ConfigureAwait(false);
        AddToCache(new CacheKey(propertyName, nodeOffset), node);
        return node;
    }

    #endregion

    #region Concurrency Helpers

    private AsyncLock GetLock(string key) => locks.GetOrAdd(key, _ => new AsyncLock());

    private static string GetIndexLockKey(string propertyName) => $"Index_{propertyName}";
    
    private static string GetDataLockKey(IComparable logicalKey, string propertyName) => $"Data_{logicalKey.ToString()}_{propertyName}";

    private readonly record struct CacheKey(string PropertyName, long Offset);
    private readonly record struct NodeWriteResult(long Offset, BTreeHeader Header);
    private readonly record struct StreamWriteResult(long Offset, long Length, DataStreamHeader Header);
    private readonly record struct BorrowResult(BTreeHeader Header, long SiblingOffset, long ChildOffset);
    private readonly record struct MergeResult(BTreeHeader Header, long MergedOffset, BPlusTreeNode MergedNode);

    private sealed class AsyncLock
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);

        public async Task<IDisposable> LockAsync(CancellationToken ct)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            return new Releaser(this);
        }

        private void Release()
        {
            semaphore.Release();
        }

        private readonly struct Releaser : IDisposable
        {
            private readonly AsyncLock parent;

            public Releaser(AsyncLock parent)
            {
                this.parent = parent;
            }

            public void Dispose()
            {
                parent.Release();
            }
        }
    }

    #endregion
}