namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning.Serialization;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Manages a CRDT document that is partitioned, allowing it to scale beyond available memory by storing data and an index in streams.
/// It uses a user-friendly API with property names and translates them to internal property paths for strategy execution.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class PartitionManager<T> : IPartitionManager<T> where T : class, new()
{
    private readonly IPartitioningStrategy partitioningStrategy;
    private readonly ICrdtApplicator crdtApplicator;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly IPartitionStreamProvider streamProvider;
    private readonly IPartitionSerializationService serializationService;
    private readonly PartitionManagerCrdtMetrics metrics;

    private readonly PropertyInfo partitionKeyProperty;
    private readonly IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)> partitionableProperties;

    private const int MaxPartitionDataSize = 8192;
    private const int MinPartitionDataSize = MaxPartitionDataSize / 4;

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, (PropertyInfo, IPartitionableCrdtStrategy)>> partitionablePropertyCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> partitionKeyCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, string>> propertyNamePathCache = new();

    public PartitionManager(
        IPartitioningStrategy partitioningStrategy,
        ICrdtApplicator crdtApplicator,
        ICrdtMetadataManager metadataManager,
        ICrdtStrategyProvider strategyProvider,
        IPartitionSerializationService serializationService,
        IServiceProvider serviceProvider,
        ReplicaContext replicaContext,
        PartitionManagerCrdtMetrics metrics)
    {
        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException($"The service '{nameof(PartitionManager<T>)}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}.");
        }

        this.streamProvider = serviceProvider.GetService<IPartitionStreamProvider>() ??
            throw new InvalidOperationException(
                $"No implementation for '{nameof(IPartitionStreamProvider)}' was found. " +
                $"When using partitioning features, you must register a custom stream provider. " +
                $"Use the 'services.AddCrdtPartitionStreamProvider<TProvider>()' extension method to register your implementation.");

        this.partitioningStrategy = partitioningStrategy;
        this.crdtApplicator = crdtApplicator;
        this.metadataManager = metadataManager;
        this.serializationService = serializationService;
        this.metrics = metrics;

        partitionKeyProperty = partitionKeyCache.GetOrAdd(typeof(T), FindPartitionKeyProperty);
        partitionableProperties = partitionablePropertyCache.GetOrAdd(typeof(T), _ => FindPartitionablePropertiesAndStrategies(strategyProvider));
        propertyNamePathCache.GetOrAdd(typeof(T), _ => partitionableProperties.ToDictionary(kvp => kvp.Value.Property.Name, kvp => kvp.Key));
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(T initialObject)
    {
        using var _ = new MetricTimer(metrics.InitializationDuration);
        ArgumentNullException.ThrowIfNull(initialObject);

        var logicalKey = GetLogicalKey(initialObject);
        var headerObject = serializationService.CloneObject(initialObject)!;

        foreach (var (path, (prop, _)) in partitionableProperties)
        {
            await partitioningStrategy.InitializePropertyIndexAsync(prop.Name);
            var collection = prop.GetValue(headerObject);
            if (collection is IList list) list.Clear();
            else if (collection is IDictionary dict) dict.Clear();
        }
        
        var headerDataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey);
        headerDataStream.SetLength(0);
        await partitioningStrategy.InitializeHeaderIndexAsync();

        var headerMetadata = metadataManager.Initialize(headerObject);
        var headerDataWriteResult = await WriteToStreamAsync(headerDataStream, headerObject);
        var headerMetaWriteResult = await WriteToStreamAsync(headerDataStream, headerMetadata);
        var headerPartitionKey = new CompositePartitionKey(logicalKey, null);
        var headerPartition = new HeaderPartition(headerPartitionKey, headerDataWriteResult.Offset, headerDataWriteResult.Length, headerMetaWriteResult.Offset, headerMetaWriteResult.Length);
        await partitioningStrategy.InsertHeaderPartitionAsync(headerPartition);

        foreach (var (path, (prop, strategy)) in partitionableProperties)
        {
            var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, prop.Name);
            dataStream.SetLength(0);

            var initialCollection = prop.GetValue(initialObject);
            var dataObject = new T();
            partitionKeyProperty.SetValue(dataObject, logicalKey);
            prop.SetValue(dataObject, initialCollection);
            var dataMetadata = metadataManager.Initialize(dataObject);
        
            var dataDataWriteResult = await WriteToStreamAsync(dataStream, dataObject);
            var dataMetaWriteResult = await WriteToStreamAsync(dataStream, dataMetadata);
            var startRangeKey = strategy.GetStartKey(initialObject, prop) ?? strategy.GetMinimumKey(prop);
            
            var dataPartitionKey = new CompositePartitionKey(logicalKey, startRangeKey);
            var dataPartition = new DataPartition(dataPartitionKey, null, dataDataWriteResult.Offset, dataDataWriteResult.Length, dataMetaWriteResult.Offset, dataMetaWriteResult.Length);
            await partitioningStrategy.InsertPropertyPartitionAsync(dataPartition, prop.Name);

            if (dataPartition.DataLength > MaxPartitionDataSize)
            {
                await SplitPartitionAsync(dataPartition, prop.Name, strategy, prop, dataStream);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ApplyPatchAsync(CrdtPatch patch)
    {
        using var _ = new MetricTimer(metrics.ApplyPatchDuration);
        if (patch.Operations is null || !patch.Operations.Any()) return;
        if (patch.LogicalKey is null)
        {
            throw new ArgumentException("Patch must have a LogicalKey for partitioned documents.", nameof(patch));
        }
        
        metrics.PatchesApplied.Add(1);

        var (headerOps, propertyOps) = GroupOperationsByProperty(patch.Operations);

        if (headerOps.Count > 0)
        {
            await ApplyToHeaderPartitionAsync(patch.LogicalKey, headerOps);
        }

        foreach (var (propertyName, operations) in propertyOps)
        {
            await ApplyToDataPartitionsAsync(patch.LogicalKey, propertyName, operations);
        }
    }
    
    /// <inheritdoc/>
    public async Task<T?> GetFullObjectAsync(IComparable logicalKey)
    {
        using var _ = new MetricTimer(metrics.GetFullObjectDuration);
        ArgumentNullException.ThrowIfNull(logicalKey);

        var headerDoc = await GetHeaderPartitionContentAsync(logicalKey);
        if (headerDoc is null) return null;

        var fullObject = headerDoc.Value.Data!;

        foreach (var (path, (prop, _)) in partitionableProperties)
        {
            var collection = prop.GetValue(fullObject);
            if (collection is IList list) list.Clear();
            else if (collection is IDictionary dict) dict.Clear();

            await foreach(var partition in GetAllDataPartitionsAsync(logicalKey, prop.Name))
            {
                var partitionDoc = await GetDataPartitionContentAsync(partition.GetPartitionKey(), prop.Name);
                var partitionCollection = prop.GetValue(partitionDoc!.Value.Data!);
                
                if (collection is IList partitionList && partitionCollection is IEnumerable penum)
                {
                    foreach(var item in penum) partitionList.Add(item);
                }
                else if (collection is IDictionary dict && partitionCollection is IDictionary pdict)
                {
                    foreach (DictionaryEntry item in pdict) dict.Add(item.Key, item.Value);
                }
            }
        }

        return fullObject;
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetHeaderPartitionAsync(IComparable logicalKey)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        var headerKey = new CompositePartitionKey(logicalKey, null);
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        return await partitioningStrategy.FindHeaderPartitionAsync(headerKey);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetHeaderPartitionContentAsync(IComparable logicalKey)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);

        var headerPartition = await GetHeaderPartitionAsync(logicalKey);
        if (headerPartition is null) return null;

        var dataStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey);
        return await LoadPartitionContentAsync(headerPartition, dataStream);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionAsync(CompositePartitionKey key, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        return await partitioningStrategy.FindPropertyPartitionAsync(key, propertyName);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetDataPartitionContentAsync(CompositePartitionKey key, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);
        var partition = await partitioningStrategy.FindPropertyPartitionAsync(key, propertyName);

        if (partition is not DataPartition) return null;

        var dataStream = await streamProvider.GetPropertyDataStreamAsync(key.LogicalKey, propertyName);
        var dataDoc = await LoadPartitionContentAsync(partition, dataStream);

        var headerDoc = await GetHeaderPartitionContentAsync(key.LogicalKey);
        if (headerDoc is null)
        {
            throw new InvalidOperationException($"Could not find header partition for logical key '{key.LogicalKey}'.");
        }

        var propertyPath = ToPropertyPath(propertyName);
        var (prop, _) = partitionableProperties[propertyPath];
        var collection = prop.GetValue(dataDoc.Data);
        prop.SetValue(headerDoc.Value.Data, collection);

        var mergedMeta = metadataManager.Merge(headerDoc.Value.Metadata!, dataDoc.Metadata!);
        return new CrdtDocument<T>(headerDoc.Value.Data, mergedMeta);
    }
    
    /// <inheritdoc/>
    public async IAsyncEnumerable<IPartition> GetAllDataPartitionsAsync(IComparable logicalKey, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetAllDataPartitionsDuration);
        ArgumentNullException.ThrowIfNull(logicalKey);
        
        await foreach (var partition in partitioningStrategy.GetAllPropertyPartitionsAsync(propertyName, logicalKey))
        {
            if (partition is DataPartition dataPartition)
            {
                yield return dataPartition;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetDataPartitionCountAsync(IComparable logicalKey, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetDataPartitionCountDuration);
        ArgumentNullException.ThrowIfNull(logicalKey);
        return await partitioningStrategy.GetPropertyPartitionCountAsync(propertyName, logicalKey);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetDataPartitionByIndexDuration);
        ArgumentNullException.ThrowIfNull(logicalKey);
        if (index < 0) return null;
        
        return await partitioningStrategy.GetPropertyPartitionByIndexAsync(logicalKey, index, propertyName);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync()
    {
        using var _ = new MetricTimer(metrics.GetAllLogicalKeysDuration);
        await partitioningStrategy.InitializeHeaderIndexAsync();
        
        var logicalKeys = new HashSet<IComparable>();
        await foreach (var partition in partitioningStrategy.GetAllHeaderPartitionsAsync())
        {
            logicalKeys.Add(partition.GetPartitionKey().LogicalKey);
        }
        return logicalKeys;
    }
    
    private async Task ApplyToHeaderPartitionAsync(IComparable logicalKey, IEnumerable<CrdtOperation> operations)
    {
        var headerDoc = await GetHeaderPartitionContentAsync(logicalKey)
            ?? throw new InvalidOperationException($"Could not find header partition for logical key '{logicalKey}'.");
        var headerPartition = await GetHeaderPartitionAsync(logicalKey);
        
        var headerStream = await streamProvider.GetHeaderDataStreamAsync(logicalKey);
        
        crdtApplicator.ApplyPatch(headerDoc, new CrdtPatch(operations.ToList().AsReadOnly()) { LogicalKey = logicalKey });
        
        await PersistPartitionChangesAsync(headerPartition!, headerDoc.Data!, headerDoc.Metadata!, headerStream, null);
    }
    
    private async Task ApplyToDataPartitionsAsync(IComparable logicalKey, string propertyName, IEnumerable<CrdtOperation> operations)
    {
        var propertyPath = ToPropertyPath(propertyName);
        var (prop, strategy) = partitionableProperties[propertyPath];
        var dataStream = await streamProvider.GetPropertyDataStreamAsync(logicalKey, propertyName);

        var opsByPartition = await GroupOperationsByPartitionAsync(logicalKey, propertyName, strategy, prop, operations);
        
        foreach(var (partition, ops) in opsByPartition)
        {
            var crdtDoc = await GetDataPartitionContentAsync(partition.GetPartitionKey(), prop.Name);

            using (new MetricTimer(metrics.ApplicatorApplyPatchDuration))
            {
                crdtApplicator.ApplyPatch(crdtDoc!.Value, new CrdtPatch(ops) { LogicalKey = logicalKey });
            }
            
            var updatedPartition = await PersistPartitionChangesAsync(partition, crdtDoc.Value.Data!, crdtDoc.Value.Metadata!, dataStream, propertyName);
            
            if (updatedPartition is DataPartition)
            {
                if (updatedPartition.DataLength > MaxPartitionDataSize)
                {
                    await SplitPartitionAsync(updatedPartition, propertyName, strategy, prop, dataStream);
                }
                else if (updatedPartition.DataLength < MinPartitionDataSize)
                {
                    var partitionCount = await partitioningStrategy.GetPropertyPartitionCountAsync(propertyName, logicalKey);
                    if (partitionCount > 1)
                    {
                        await MergePartitionIfNeededAsync(updatedPartition, propertyName, strategy, prop, dataStream);
                    }
                }
            }
        }
    }
    
    private (List<CrdtOperation> headerOps, Dictionary<string, List<CrdtOperation>> propertyOps) GroupOperationsByProperty(IEnumerable<CrdtOperation> operations)
    {
        var propertyOps = new Dictionary<string, List<CrdtOperation>>();
        var headerOps = new List<CrdtOperation>();
        foreach(var op in operations)
        {
            var propertyName = GetPropertyNameFromOperation(op);
            if (propertyName is null)
            {
                headerOps.Add(op);
            }
            else
            {
                if (!propertyOps.TryGetValue(propertyName, out var list))
                {
                    list = new List<CrdtOperation>();
                    propertyOps[propertyName] = list;
                }
                list.Add(op);
            }
        }
        return (headerOps, propertyOps);
    }
    
    private string? GetPropertyNameFromOperation(CrdtOperation op)
    {
        if (string.IsNullOrEmpty(op.JsonPath) || op.JsonPath == "$")
        {
            return null;
        }

        var segments = PocoPathHelper.ParsePath(op.JsonPath);

        if (segments.Length == 0)
        {
            return null;
        }

        var propertyName = segments[0];
        var fullPath = $"$.{propertyName}";

        return partitionableProperties.TryGetValue(fullPath, out var val) ? val.Property.Name : null;
    }

    private async Task<Dictionary<IPartition, List<CrdtOperation>>> GroupOperationsByPartitionAsync(IComparable logicalKey, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, IEnumerable<CrdtOperation> operations)
    {
        using var _ = new MetricTimer(metrics.GroupOperationsDuration);
        var opsByPartition = new Dictionary<IPartition, List<CrdtOperation>>();
        
        var propertyPath = ToPropertyPath(propertyName);

        foreach (var op in operations)
        {
            var rangeKey = strategy.GetKeyFromOperation(op, propertyPath);
            var compositeKey = new CompositePartitionKey(logicalKey, rangeKey);

            var partition = await partitioningStrategy.FindPropertyPartitionAsync(compositeKey, propertyName)
                ?? throw new InvalidOperationException($"Could not find partition for key '{compositeKey}' in property '{propertyName}'.");

            if (!opsByPartition.TryGetValue(partition, out var opList))
            {
                opList = new List<CrdtOperation>();
                opsByPartition[partition] = opList;
            }
            opList.Add(op);
        }
        return opsByPartition;
    }

    private readonly record struct StreamWriteResult(long Offset, long Length);

    private async Task SplitPartitionAsync(IPartition partitionToSplit, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, Stream dataStream)
    {
        using var _ = new MetricTimer(metrics.SplitPartitionDuration);
        if (partitionToSplit is not DataPartition dataPartitionToSplit) return;
        
        metrics.PartitionsSplit.Add(1);

        var crdtDoc = await GetDataPartitionContentAsync(dataPartitionToSplit.GetPartitionKey(), prop.Name);
        
        SplitResult splitResult;
        using (new MetricTimer(metrics.StrategySplitDuration))
        {
            splitResult = strategy.Split(crdtDoc!.Value.Data!, crdtDoc.Value.Metadata!, prop);
        }

        var p1DataWrite = await WriteToStreamAsync(dataStream, splitResult.Partition1.Data);
        var p1MetaWrite = await WriteToStreamAsync(dataStream, splitResult.Partition1.Metadata);
        var p2DataWrite = await WriteToStreamAsync(dataStream, splitResult.Partition2.Data);
        var p2MetaWrite = await WriteToStreamAsync(dataStream, splitResult.Partition2.Metadata);
        
        var originalKey = dataPartitionToSplit.StartKey;

        var p1Key = originalKey;
        var p2Key = new CompositePartitionKey(originalKey.LogicalKey, splitResult.SplitKey);

        var p1 = new DataPartition(p1Key, p2Key, p1DataWrite.Offset, p1DataWrite.Length, p1MetaWrite.Offset, p1MetaWrite.Length);
        var p2 = new DataPartition(p2Key, dataPartitionToSplit.EndKey, p2DataWrite.Offset, p2DataWrite.Length, p2MetaWrite.Offset, p2MetaWrite.Length);
        
        await partitioningStrategy.DeletePropertyPartitionAsync(dataPartitionToSplit, propertyName);
        await partitioningStrategy.InsertPropertyPartitionAsync(p1, propertyName);
        await partitioningStrategy.InsertPropertyPartitionAsync(p2, propertyName);
    }

    private async Task MergePartitionIfNeededAsync(IPartition partitionToMerge, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, Stream dataStream)
    {
        using var _ = new MetricTimer(metrics.MergePartitionDuration);
        if (partitionToMerge is not DataPartition dataPartitionToMerge) return;

        var logicalKey = dataPartitionToMerge.StartKey.LogicalKey;
        
        var logicalPartitions = new List<DataPartition>();
        await foreach(var p in partitioningStrategy.GetAllPropertyPartitionsAsync(propertyName, logicalKey))
        {
            if (p is DataPartition dp)
            {
                logicalPartitions.Add(dp);
            }
        }
        
        var partitionIndex = logicalPartitions.FindIndex(p => p.StartKey.Equals(dataPartitionToMerge.StartKey));
        if (partitionIndex == -1) return;

        int targetIndex;
        int sourceIndex;

        if (partitionIndex > 0)
        {
            targetIndex = partitionIndex - 1;
            sourceIndex = partitionIndex;
        }
        else 
        {
            if (logicalPartitions.Count < 2) return;
            targetIndex = partitionIndex;
            sourceIndex = partitionIndex + 1;
        }

        var targetPartition = logicalPartitions[targetIndex];
        var sourcePartition = logicalPartitions[sourceIndex];
        
        var targetDocument = await GetDataPartitionContentAsync(targetPartition.GetPartitionKey(), prop.Name);
        var sourceDocument = await GetDataPartitionContentAsync(sourcePartition.GetPartitionKey(), prop.Name);
        
        var mergedContent = strategy.Merge(targetDocument!.Value.Data!, targetDocument.Value.Metadata!, sourceDocument!.Value.Data!, sourceDocument.Value.Metadata!, prop);
        
        var dataWriteResult = await WriteToStreamAsync(dataStream, mergedContent.Data);
        var metaWriteResult = await WriteToStreamAsync(dataStream, mergedContent.Metadata);
        
        var mergedPartition = new DataPartition(targetPartition.StartKey, sourcePartition.EndKey, dataWriteResult.Offset, dataWriteResult.Length, metaWriteResult.Offset, metaWriteResult.Length);

        await partitioningStrategy.DeletePropertyPartitionAsync(targetPartition, propertyName);
        await partitioningStrategy.DeletePropertyPartitionAsync(sourcePartition, propertyName);
        await partitioningStrategy.InsertPropertyPartitionAsync(mergedPartition, propertyName);
        
        metrics.PartitionsMerged.Add(1);
    }

    private IComparable GetLogicalKey(T obj)
    {
        var logicalKeyObj = partitionKeyProperty.GetValue(obj) ?? throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' cannot be null.");
        if (logicalKeyObj is not IComparable logicalKey)
        {
            throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' must implement IComparable.");
        }
        return logicalKey;
    }
    
    private static IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)> FindPartitionablePropertiesAndStrategies(ICrdtStrategyProvider strategyProvider)
    {
        var partitionableProperties = typeof(T).GetProperties()
            .Select(p => new
            {
                Property = p,
                Strategy = strategyProvider.GetStrategy(p),
                Path = $"$.{char.ToLowerInvariant(p.Name[0])}{p.Name[1..]}"
            })
            .Where(x => x.Strategy is IPartitionableCrdtStrategy)
            .ToDictionary(x => x.Path, x => (x.Property, (IPartitionableCrdtStrategy)x.Strategy!));

        if (partitionableProperties.Count == 0)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' does not have any properties with a CRDT strategy that supports partitioning (implements {nameof(IPartitionableCrdtStrategy)}).");
        }
        
        return partitionableProperties;
    }
    
    private static PropertyInfo FindPartitionKeyProperty(Type type)
    {
        var attr = type.GetCustomAttribute<PartitionKeyAttribute>();
        if (attr is null)
        {
            throw new NotSupportedException($"The type '{type.Name}' must be decorated with the [{nameof(PartitionKeyAttribute)}] to be used with partitioning.");
        }
        
        var property = type.GetProperty(attr.PropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
        {
            throw new NotSupportedException($"The partition key property '{attr.PropertyName}' specified on type '{type.Name}' was not found.");
        }
        
        return property;
    }

    private async Task<CrdtDocument<T>> LoadPartitionContentAsync(IPartition partition, Stream dataStream)
    {
        using var _ = new MetricTimer(metrics.StreamReadDuration);
        var docBuffer = new byte[partition.DataLength];
        dataStream.Seek(partition.DataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(docBuffer);
        using var docStream = new MemoryStream(docBuffer);
        var doc = await serializationService.DeserializeObjectAsync<T>(docStream);
        
        var metaBuffer = new byte[partition.MetadataLength];
        dataStream.Seek(partition.MetadataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(metaBuffer);
        using var metaStream = new MemoryStream(metaBuffer);
        var meta = await serializationService.DeserializeObjectAsync<CrdtMetadata>(metaStream);

        return new CrdtDocument<T>(doc!, meta!);
    }
    
    private async Task<StreamWriteResult> WriteToStreamAsync(Stream dataStream, object content)
    {
        using var _ = new MetricTimer(metrics.StreamWriteDuration);
        dataStream.Seek(0, SeekOrigin.End);
        var offset = dataStream.Position;
        await serializationService.SerializeObjectAsync(dataStream, content);
        await dataStream.FlushAsync();
        var length = dataStream.Position - offset;
        return new StreamWriteResult(offset, length);
    }
    
    private async Task<IPartition> PersistPartitionChangesAsync(IPartition partitionToUpdate, T newData, CrdtMetadata newMeta, Stream dataStream, string? propertyName)
    {
        using var _ = new MetricTimer(metrics.PersistChangesDuration);
        var newDataWriteResult = await WriteToStreamAsync(dataStream, newData);
        var newMetaWriteResult = await WriteToStreamAsync(dataStream, newMeta);

        IPartition updatedPartition = partitionToUpdate switch
        {
            HeaderPartition hp => hp with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length },
            DataPartition dp => dp with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length },
            _ => throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}")
        };
        
        if (updatedPartition is HeaderPartition)
        {
            await partitioningStrategy.UpdateHeaderPartitionAsync(updatedPartition);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            await partitioningStrategy.UpdatePropertyPartitionAsync(updatedPartition, propertyName);
        }

        return updatedPartition;
    }

    private string ToPropertyPath(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (!propertyNamePathCache.TryGetValue(typeof(T), out var nameToPathMap) || !nameToPathMap.TryGetValue(propertyName, out var propertyPath))
        {
            throw new ArgumentException($"Property '{propertyName}' is not a partitionable property on type '{typeof(T).Name}'.", nameof(propertyName));
        }
        return propertyPath;
    }
}