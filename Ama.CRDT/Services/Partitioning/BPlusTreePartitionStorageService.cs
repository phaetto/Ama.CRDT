namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
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
        
        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, cancellationToken);
        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, cancellationToken);

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
        
        var newDataWriteResult = await WriteToStreamAsync(dataStream, data, cancellationToken);
        var newMetaWriteResult = await WriteToStreamAsync(dataStream, metadata, cancellationToken);

        return partitionToUpdate with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length };
    }

    private async Task<(long Offset, long Length)> WriteToStreamAsync(Stream dataStream, object content, CancellationToken cancellationToken)
    {
        dataStream.Seek(0, SeekOrigin.End);
        var offset = dataStream.Position;
        await serializationService.SerializeObjectAsync(dataStream, content); // Removed the cancellationToken argument to match the interface
        await dataStream.FlushAsync(cancellationToken);
        var length = dataStream.Position - offset;
        return (offset, length);
    }

    /// <inheritdoc/>
    public async Task ClearPropertyDataAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        var stream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName);
        stream.SetLength(0);
    }

    /// <inheritdoc/>
    public async Task ClearHeaderDataAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        var stream = await streamProvider.GetHeaderDataStreamAsync(logicalKey);
        stream.SetLength(0);
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