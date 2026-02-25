namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An implementation of <see cref="IPartitionStorageService"/> that coordinates streams and B+ Tree indexing to store partition data.
/// </summary>
public sealed class BPlusTreePartitionStorageService : IPartitionStorageService
{
    private readonly IPartitionStreamProvider streamProvider;
    private readonly IPartitioningStrategy partitioningStrategy;
    private readonly IPartitionSerializationService serializationService;
    private readonly PartitionManagerCrdtMetrics metrics;

    public BPlusTreePartitionStorageService(
        IServiceProvider serviceProvider,
        IPartitioningStrategy partitioningStrategy,
        IPartitionSerializationService serializationService,
        PartitionManagerCrdtMetrics metrics)
    {
        this.streamProvider = serviceProvider.GetService<IPartitionStreamProvider>() ??
            throw new InvalidOperationException(
                $"No implementation for '{nameof(IPartitionStreamProvider)}' was found. " +
                $"When using partitioning features, you must register a custom stream provider. " +
                $"Use the 'services.AddCrdtPartitionStreamProvider<TProvider>()' extension method to register your implementation.");
        
        this.partitioningStrategy = partitioningStrategy;
        this.serializationService = serializationService;
        this.metrics = metrics;
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<TData>> LoadPartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partition, CancellationToken cancellationToken = default) where TData : class, new()
    {
        using var _ = new MetricTimer(metrics.StreamReadDuration);
        var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName);
        return await LoadContentInternalAsync<TData>(partition, dataStream);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<TData>> LoadHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partition, CancellationToken cancellationToken = default) where TData : class, new()
    {
        using var _ = new MetricTimer(metrics.StreamReadDuration);
        var dataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey);
        return await LoadContentInternalAsync<TData>(partition, dataStream);
    }

    private async Task<CrdtDocument<TData>> LoadContentInternalAsync<TData>(IPartition partition, Stream dataStream) where TData : class, new()
    {
        var docBuffer = new byte[partition.DataLength];
        dataStream.Seek(partition.DataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(docBuffer);
        using var docStream = new MemoryStream(docBuffer);
        var doc = await serializationService.DeserializeObjectAsync<TData>(docStream);
        
        var metaBuffer = new byte[partition.MetadataLength];
        dataStream.Seek(partition.MetadataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(metaBuffer);
        using var metaStream = new MemoryStream(metaBuffer);
        var meta = await serializationService.DeserializeObjectAsync<CrdtMetadata>(metaStream);

        return new CrdtDocument<TData>(doc!, meta!);
    }

    /// <inheritdoc/>
    public async Task<IPartition> SavePartitionContentAsync<TData>(IComparable logicalKey, string propertyName, IPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class, new()
    {
        using var _ = new MetricTimer(metrics.StreamWriteDuration);
        var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName);
        var header = await ReadOrCreateDataHeaderAsync(dataStream);
        
        long oldDataOffset = partitionToUpdate is DataPartition dp1 && dp1.DataOffset > 0 ? dp1.DataOffset : -1;
        long oldDataLength = partitionToUpdate.DataLength;
        long oldMetaOffset = partitionToUpdate is DataPartition dp2 && dp2.MetadataOffset > 0 ? dp2.MetadataOffset : -1;
        long oldMetaLength = partitionToUpdate.MetadataLength;

        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, header, oldDataOffset, oldDataLength, cancellationToken);
        header = newDataWriteResult.Header;

        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, header, oldMetaOffset, oldMetaLength, cancellationToken);
        header = newMetaWriteResult.Header;

        await WriteDataHeaderAsync(dataStream, header);

        return partitionToUpdate switch
        {
            DataPartition dp => dp with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length },
            _ => throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}")
        };
    }

    /// <inheritdoc/>
    public async Task<HeaderPartition> SaveHeaderPartitionContentAsync<TData>(IComparable logicalKey, HeaderPartition partitionToUpdate, TData data, CrdtMetadata metadata, CancellationToken cancellationToken = default) where TData : class, new()
    {
        using var _ = new MetricTimer(metrics.StreamWriteDuration);
        var dataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey);
        var header = await ReadOrCreateDataHeaderAsync(dataStream);
        
        long oldDataOffset = partitionToUpdate.DataOffset > 0 ? partitionToUpdate.DataOffset : -1;
        long oldDataLength = partitionToUpdate.DataLength;
        long oldMetaOffset = partitionToUpdate.MetadataOffset > 0 ? partitionToUpdate.MetadataOffset : -1;
        long oldMetaLength = partitionToUpdate.MetadataLength;

        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, header, oldDataOffset, oldDataLength, cancellationToken);
        header = newDataWriteResult.Header;

        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, header, oldMetaOffset, oldMetaLength, cancellationToken);
        header = newMetaWriteResult.Header;

        await WriteDataHeaderAsync(dataStream, header);

        return partitionToUpdate with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length };
    }

    private async Task<(long Offset, long Length, DataStreamHeader Header)> WriteToStreamAsync(
        Stream dataStream, object content, DataStreamHeader header, long oldOffset, long oldLength, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await serializationService.SerializeObjectAsync(ms, content);
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

    private async Task<DataStreamHeader> ReadOrCreateDataHeaderAsync(Stream stream)
    {
        if (stream.Length < 1024)
        {
            var newHeader = new DataStreamHeader();
            await WriteDataHeaderAsync(stream, newHeader);
            return newHeader;
        }

        var buffer = new byte[1024];
        stream.Seek(0, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer);
        var jsonString = Encoding.UTF8.GetString(buffer).TrimEnd();
        
        if (string.IsNullOrWhiteSpace(jsonString)) 
        {
            var newHeader = new DataStreamHeader();
            await WriteDataHeaderAsync(stream, newHeader);
            return newHeader;
        }

        return JsonSerializer.Deserialize<DataStreamHeader>(jsonString) ?? new DataStreamHeader();
    }

    private async Task WriteDataHeaderAsync(Stream stream, DataStreamHeader header)
    {
        var jsonString = JsonSerializer.Serialize(header);
        var buffer = Encoding.UTF8.GetBytes(jsonString);
        if (buffer.Length > 1024) throw new InvalidOperationException("Data stream header exceeded 1024 bytes.");

        var padded = new byte[1024];
        Array.Fill(padded, (byte)' ');
        Array.Copy(buffer, padded, buffer.Length);

        stream.Seek(0, SeekOrigin.Begin);
        await stream.WriteAsync(padded);
        await stream.FlushAsync();
    }

    /// <inheritdoc/>
    public async Task ClearPropertyDataAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        var stream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName);
        stream.SetLength(0);
        await ReadOrCreateDataHeaderAsync(stream);
    }

    /// <inheritdoc/>
    public async Task ClearHeaderDataAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        var stream = await streamProvider.GetHeaderDataStreamAsync(logicalKey);
        stream.SetLength(0);
        await ReadOrCreateDataHeaderAsync(stream);
    }

    /// <inheritdoc/>
    public Task InitializePropertyIndexAsync(string propertyName, CancellationToken cancellationToken = default) => partitioningStrategy.InitializePropertyIndexAsync(propertyName);
    
    /// <inheritdoc/>
    public Task InitializeHeaderIndexAsync(CancellationToken cancellationToken = default) => partitioningStrategy.InitializeHeaderIndexAsync();
    
    /// <inheritdoc/>
    public Task InsertPropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) => partitioningStrategy.InsertPropertyPartitionAsync(partition, propertyName);
    
    /// <inheritdoc/>
    public Task InsertHeaderPartitionAsync(IComparable logicalKey, HeaderPartition headerPartition, CancellationToken cancellationToken = default) => partitioningStrategy.InsertHeaderPartitionAsync(headerPartition);
    
    /// <inheritdoc/>
    public Task UpdatePropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) => partitioningStrategy.UpdatePropertyPartitionAsync(partition, propertyName);
    
    /// <inheritdoc/>
    public Task UpdateHeaderPartitionAsync(IComparable logicalKey, HeaderPartition headerPartition, CancellationToken cancellationToken = default) => partitioningStrategy.UpdateHeaderPartitionAsync(headerPartition);
    
    /// <inheritdoc/>
    public Task DeletePropertyPartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default) => partitioningStrategy.DeletePropertyPartitionAsync(partition, propertyName);
    
    /// <inheritdoc/>
    public IAsyncEnumerable<IPartition> GetPartitionsAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) => partitioningStrategy.GetAllPropertyPartitionsAsync(propertyName, logicalKey);
    
    /// <inheritdoc/>
    public Task<IPartition?> GetPropertyPartitionAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default) => partitioningStrategy.FindPropertyPartitionAsync(key, propertyName);
    
    /// <inheritdoc/>
    public Task<long> GetPropertyPartitionCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) => partitioningStrategy.GetPropertyPartitionCountAsync(propertyName, logicalKey);
    
    /// <inheritdoc/>
    public Task<IPartition?> GetPropertyPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default) => partitioningStrategy.GetPropertyPartitionByIndexAsync(logicalKey, index, propertyName);

    /// <inheritdoc/>
    public async Task<HeaderPartition?> GetHeaderPartitionAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        var result = await partitioningStrategy.FindHeaderPartitionAsync(new CompositePartitionKey(logicalKey, null));
        return result is HeaderPartition hp ? hp : null;
    }
    
    /// <inheritdoc/>
    public IAsyncEnumerable<IPartition> GetAllHeaderPartitionsAsync(CancellationToken cancellationToken = default) => partitioningStrategy.GetAllHeaderPartitionsAsync(null);
}