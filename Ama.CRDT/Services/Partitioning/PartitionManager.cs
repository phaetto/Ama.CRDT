namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Manages a CRDT document that is partitioned, allowing it to scale beyond available memory by storing data and an index in streams.
/// This implementation currently supports partitioning for properties of type <see cref="IDictionary"/>.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class PartitionManager<T> : IPartitionManager<T> where T : class, new()
{
    private readonly IPartitioningStrategy partitioningStrategy;
    private readonly ICrdtApplicator crdtApplicator;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtStrategyProvider strategyProvider;
    private readonly PropertyInfo partitionableProperty;
    private readonly IPartitionableCrdtStrategy partitionableStrategy;

    private Stream? dataStream;
    private Stream? indexStream;

    // TODO: Should be configurable (ReplicaContext probably)
    private const int MaxPartitionDataSize = 8192; // 8 KB
    private const int MinPartitionDataSize = MaxPartitionDataSize / 4; // 2 KB

    public PartitionManager(
        IPartitioningStrategy partitioningStrategy, 
        ICrdtApplicator crdtApplicator, 
        ICrdtMetadataManager metadataManager,
        ICrdtStrategyProvider strategyProvider,
        ReplicaContext replicaContext)
    {
        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException($"The service '{nameof(PartitionManager<T>)}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}.");
        }
        this.partitioningStrategy = partitioningStrategy;
        this.crdtApplicator = crdtApplicator;
        this.metadataManager = metadataManager;
        this.strategyProvider = strategyProvider;

        var partitionInfo = FindPartitionablePropertyAndStrategy();
        partitionableProperty = partitionInfo.Property;
        partitionableStrategy = partitionInfo.Strategy;
    }
    
    /// <inheritdoc/>
    public async Task InitializeAsync(Stream dataStream, Stream indexStream, T initialObject)
    {
        ArgumentNullException.ThrowIfNull(dataStream);
        ArgumentNullException.ThrowIfNull(indexStream);
        ArgumentNullException.ThrowIfNull(initialObject);

        this.dataStream = dataStream;
        this.indexStream = indexStream;
        
        dataStream.SetLength(0);
        indexStream.SetLength(0);

        var metadata = metadataManager.Initialize(initialObject);
        var dataWriteResult = await WriteToStreamAsync(initialObject);
        var metaWriteResult = await WriteToStreamAsync(metadata);

        var initialStartKey = partitionableStrategy.GetStartKey(initialObject) ?? 0;

        var initialPartition = new Partition(initialStartKey, null, dataWriteResult.Offset, dataWriteResult.Length, metaWriteResult.Offset, metaWriteResult.Length);
        
        await partitioningStrategy.InitializeAsync(indexStream);
        await partitioningStrategy.InsertPartitionAsync(initialPartition);

        if (initialPartition.DataLength > MaxPartitionDataSize)
        {
            await SplitPartitionAsync(initialPartition);
        }
    }

    /// <inheritdoc/>
    public async Task ApplyPatchAsync(CrdtPatch patch)
    {
        if (dataStream is null)
        {
            throw new InvalidOperationException("PartitionManager must be initialized before applying patches.");
        }
        if (patch.Operations is null || !patch.Operations.Any()) return;

        var opsByPartition = new Dictionary<Partition, List<CrdtOperation>>();
        foreach(var op in patch.Operations)
        {
            var partition = await FindPartitionForOperationAsync(op);
            if (!opsByPartition.ContainsKey(partition))
            {
                opsByPartition[partition] = new List<CrdtOperation>();
            }
            opsByPartition[partition].Add(op);
        }
        
        foreach(var kvp in opsByPartition)
        {
            var content = await LoadPartitionContentAsync(kvp.Key);
            var crdtDoc = new CrdtDocument<T>((T)content.Data, content.Metadata);
            crdtApplicator.ApplyPatch(crdtDoc, new CrdtPatch { Operations = kvp.Value });
            
            var updatedPartition = await PersistPartitionChangesAsync(kvp.Key, crdtDoc.Data, crdtDoc.Metadata);
            
            if (updatedPartition.DataLength > MaxPartitionDataSize)
            {
                await SplitPartitionAsync(updatedPartition);
            }
            else if (updatedPartition.DataLength < MinPartitionDataSize)
            {
                var allPartitions = await partitioningStrategy.GetAllPartitionsAsync();
                if (allPartitions.Count > 1)
                {
                    await MergePartitionIfNeededAsync(updatedPartition, allPartitions);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Partition?> GetPartitionAsync(object key)
    {
        if (indexStream is null)
        {
            throw new InvalidOperationException("PartitionManager must be initialized before getting a partition.");
        }
        ArgumentNullException.ThrowIfNull(key);
        return await partitioningStrategy.FindPartitionAsync(key);
    }

    /// <inheritdoc/>
    public async Task<PartitionContent?> GetPartitionContentAsync(object key)
    {
        if (dataStream is null || indexStream is null)
        {
            throw new InvalidOperationException("PartitionManager must be initialized before getting partition content.");
        }
        ArgumentNullException.ThrowIfNull(key);

        var partition = await partitioningStrategy.FindPartitionAsync(key);
        if (partition is null)
        {
            return null;
        }

        return await LoadPartitionContentAsync(partition.Value);
    }

    private readonly record struct PartitionablePropertyInfo(PropertyInfo Property, IPartitionableCrdtStrategy Strategy);
    private readonly record struct StreamWriteResult(long Offset, long Length);

    private async Task SplitPartitionAsync(Partition partitionToSplit)
    {
        var content = await LoadPartitionContentAsync(partitionToSplit);
        var doc = content.Data;
        var meta = content.Metadata;

        var splitResult = partitionableStrategy.Split(doc, meta, typeof(T));

        var p1DataWrite = await WriteToStreamAsync(splitResult.Partition1.Data);
        var p1MetaWrite = await WriteToStreamAsync(splitResult.Partition1.Metadata);
        var p2DataWrite = await WriteToStreamAsync(splitResult.Partition2.Data);
        var p2MetaWrite = await WriteToStreamAsync(splitResult.Partition2.Metadata);

        var p1 = new Partition(partitionToSplit.StartKey, splitResult.SplitKey, p1DataWrite.Offset, p1DataWrite.Length, p1MetaWrite.Offset, p1MetaWrite.Length);
        var p2 = new Partition(splitResult.SplitKey, partitionToSplit.EndKey, p2DataWrite.Offset, p2DataWrite.Length, p2MetaWrite.Offset, p2MetaWrite.Length);
        
        await partitioningStrategy.DeletePartitionAsync(partitionToSplit);
        await partitioningStrategy.InsertPartitionAsync(p1);
        await partitioningStrategy.InsertPartitionAsync(p2);
    }

    private async Task MergePartitionIfNeededAsync(Partition partitionToMerge, List<Partition> allPartitions)
    {
        var partitionIndex = allPartitions.FindIndex(p => p.StartKey.Equals(partitionToMerge.StartKey));
        if (partitionIndex == -1) return; // Partition might have been removed already by another merge

        // Determine which sibling to merge with. Prioritize merging with the previous one.
        int sourceIndex;
        int targetIndex;

        if (partitionIndex > 0) // Has a previous sibling
        {
            targetIndex = partitionIndex - 1;
            sourceIndex = partitionIndex;
        }
        else // No previous, must use next
        {
            targetIndex = partitionIndex;
            sourceIndex = partitionIndex + 1;
        }

        var targetPartition = allPartitions[targetIndex];
        var sourcePartition = allPartitions[sourceIndex];
        
        var targetContent = await LoadPartitionContentAsync(targetPartition);
        var sourceContent = await LoadPartitionContentAsync(sourcePartition);
        
        var mergedContent = partitionableStrategy.Merge(targetContent.Data, targetContent.Metadata, sourceContent.Data, sourceContent.Metadata, typeof(T));
        
        var dataWriteResult = await WriteToStreamAsync(mergedContent.Data);
        var metaWriteResult = await WriteToStreamAsync(mergedContent.Metadata);
        
        var mergedPartition = new Partition(targetPartition.StartKey, sourcePartition.EndKey, dataWriteResult.Offset, dataWriteResult.Length, metaWriteResult.Offset, metaWriteResult.Length);

        await partitioningStrategy.DeletePartitionAsync(targetPartition);
        await partitioningStrategy.DeletePartitionAsync(sourcePartition);
        await partitioningStrategy.InsertPartitionAsync(mergedPartition);
    }

    private PartitionablePropertyInfo FindPartitionablePropertyAndStrategy()
    {
        var property = typeof(T).GetProperties()
            .FirstOrDefault(p => strategyProvider.GetStrategy(p) is IPartitionableCrdtStrategy);

        if (property is null)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' does not have a property with a CRDT strategy that supports partitioning (implements {nameof(IPartitionableCrdtStrategy)}).");
        }
        
        var strategy = (IPartitionableCrdtStrategy)strategyProvider.GetStrategy(property);
        return new PartitionablePropertyInfo(property, strategy);
    }
    
    private async Task<Partition> FindPartitionForOperationAsync(CrdtOperation op)
    {
        object? key = partitionableStrategy.GetKeyFromOperation(op);
        
        if (key is null)
        {
            // Fallback for operations that are not key-based (e.g. global operations on the document)
            // or for strategies where a key cannot be determined from a single operation.
            var allPartitions = await partitioningStrategy.GetAllPartitionsAsync();
            if (!allPartitions.Any()) throw new InvalidOperationException("Cannot find partition for operation as no partitions exist.");
            key = allPartitions.First().StartKey;
        }

        var foundPartition = await partitioningStrategy.FindPartitionAsync(key);
        if (foundPartition is null)
        {
            throw new InvalidOperationException($"Could not find partition for key '{key}'.");
        }
        
        return foundPartition.Value;
    }

    private async Task<PartitionContent> LoadPartitionContentAsync(Partition partition)
    {
        var docBuffer = new byte[partition.DataLength];
        dataStream!.Seek(partition.DataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(docBuffer);
        using var docStream = new MemoryStream(docBuffer);
        var doc = await JsonSerializer.DeserializeAsync<T>(docStream, CrdtJsonContext.DefaultOptions);
        
        var metaBuffer = new byte[partition.MetadataLength];
        dataStream.Seek(partition.MetadataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(metaBuffer);
        using var metaStream = new MemoryStream(metaBuffer);
        var meta = await JsonSerializer.DeserializeAsync<CrdtMetadata>(metaStream, CrdtJsonContext.DefaultOptions);

        return new PartitionContent(doc!, meta!);
    }
    
    private async Task<StreamWriteResult> WriteToStreamAsync(object content)
    {
        dataStream!.Seek(0, SeekOrigin.End);
        var offset = dataStream.Position;
        await JsonSerializer.SerializeAsync(dataStream, content, content.GetType(), CrdtJsonContext.DefaultOptions);
        var length = dataStream.Position - offset;
        return new StreamWriteResult(offset, length);
    }
    
    private async Task<Partition> PersistPartitionChangesAsync(Partition partitionToUpdate, T newData, CrdtMetadata newMeta)
    {
        var newDataWriteResult = await WriteToStreamAsync(newData);
        var newMetaWriteResult = await WriteToStreamAsync(newMeta);

        var updatedPartition = partitionToUpdate with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length };
        
        await partitioningStrategy.UpdatePartitionAsync(updatedPartition);

        return updatedPartition;
    }
}