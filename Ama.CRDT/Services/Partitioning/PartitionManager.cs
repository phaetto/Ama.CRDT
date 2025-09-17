namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// Manages a CRDT document that is partitioned, allowing it to scale beyond available memory by storing data and an index in streams.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class PartitionManager<T> : IPartitionManager<T> where T : class, new()
{
    private readonly IPartitioningStrategy partitioningStrategy;
    private readonly ICrdtApplicator crdtApplicator;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly PropertyInfo partitionableProperty;
    private readonly string partitionablePropertyPath;
    private readonly PropertyInfo partitionKeyProperty;
    private readonly IPartitionableCrdtStrategy partitionableStrategy;
    private readonly IPartitionStreamProvider streamProvider;

    private const int MaxPartitionDataSize = 8192;
    private const int MinPartitionDataSize = MaxPartitionDataSize / 4;

    public PartitionManager(
        IPartitioningStrategy partitioningStrategy,
        ICrdtApplicator crdtApplicator,
        ICrdtMetadataManager metadataManager,
        ICrdtStrategyProvider strategyProvider,
        IServiceProvider serviceProvider,
        ReplicaContext replicaContext)
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

        partitionKeyProperty = FindPartitionKeyProperty();
        (partitionableProperty, partitionableStrategy) = FindPartitionablePropertyAndStrategy(strategyProvider);
        partitionablePropertyPath = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(T initialObject)
    {
        ArgumentNullException.ThrowIfNull(initialObject);

        var logicalKey = partitionKeyProperty.GetValue(initialObject) ?? throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' cannot be null.");
        var dataStream = await streamProvider.GetDataStreamAsync(logicalKey);
        var indexStream = await streamProvider.GetIndexStreamAsync();
        
        if (indexStream.Length == 0)
        {
            dataStream.SetLength(0);
            indexStream.SetLength(0);
        }

        await partitioningStrategy.InitializeAsync();

        // 1. Create and persist the header partition (contains everything except the partitionable collection)
        var headerObject = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(initialObject, CrdtJsonContext.DefaultOptions), CrdtJsonContext.DefaultOptions)!;
        var collection = partitionableProperty.GetValue(headerObject);
        if (collection is IList list)
        {
            list.Clear();
        }
        else if (collection is IDictionary dict)
        {
            dict.Clear();
        }
        var headerMetadata = metadataManager.Initialize(headerObject);

        var headerDataWriteResult = await WriteToStreamAsync(dataStream, headerObject);
        var headerMetaWriteResult = await WriteToStreamAsync(dataStream, headerMetadata);
        var headerPartitionKey = new CompositePartitionKey(logicalKey, null);
        var headerPartition = new HeaderPartition(headerPartitionKey, headerDataWriteResult.Offset, headerDataWriteResult.Length, headerMetaWriteResult.Offset, headerMetaWriteResult.Length);
        await partitioningStrategy.InsertPartitionAsync(headerPartition);

        // 2. Create and persist the initial data partition
        var initialCollection = partitionableProperty.GetValue(initialObject);
        var dataObject = new T();
        partitionKeyProperty.SetValue(dataObject, logicalKey);
        partitionableProperty.SetValue(dataObject, initialCollection);
        var dataMetadata = metadataManager.Initialize(dataObject);
        
        var dataDataWriteResult = await WriteToStreamAsync(dataStream, dataObject);
        var dataMetaWriteResult = await WriteToStreamAsync(dataStream, dataMetadata);
        var startRangeKey = partitionableStrategy.GetStartKey(initialObject);

        // For an empty partitionable collection, the strategy can return a null start key.
        // This would collide with the header partition's key (which also uses a null range key).
        // We must create a distinct, non-null "minimum" value for the initial data partition's key.
        if (startRangeKey is null)
        {
            var keyType = GetPartitionKeyType(partitionableProperty.PropertyType);
            startRangeKey = GetMinValueForType(keyType);
        }
        
        var dataPartitionKey = new CompositePartitionKey(logicalKey, startRangeKey);
        var dataPartition = new DataPartition(dataPartitionKey, null, dataDataWriteResult.Offset, dataDataWriteResult.Length, dataMetaWriteResult.Offset, dataMetaWriteResult.Length);
        await partitioningStrategy.InsertPartitionAsync(dataPartition);

        if (dataPartition.DataLength > MaxPartitionDataSize)
        {
            await SplitPartitionAsync(dataPartition, dataStream);
        }
    }

    /// <inheritdoc/>
    public async Task ApplyPatchAsync(CrdtPatch patch)
    {
        if (patch.Operations is null || !patch.Operations.Any()) return;
        if (patch.LogicalKey is null)
        {
            throw new ArgumentException("Patch must have a LogicalKey for partitioned documents.", nameof(patch));
        }
        
        var logicalKey = patch.LogicalKey;
        var dataStream = await streamProvider.GetDataStreamAsync(logicalKey);
        var opsByPartition = await GroupOperationsByPartitionAsync(patch);
        
        foreach(var (partition, operations) in opsByPartition)
        {
            var crdtDoc = await LoadPartitionContentAsync(partition, dataStream);
            crdtApplicator.ApplyPatch(crdtDoc, new CrdtPatch(operations) { LogicalKey = patch.LogicalKey });
            
            var updatedPartition = await PersistPartitionChangesAsync(partition, crdtDoc.Data!, crdtDoc.Metadata!, dataStream);
            
            if (updatedPartition is DataPartition)
            {
                if (updatedPartition.DataLength > MaxPartitionDataSize)
                {
                    await SplitPartitionAsync(updatedPartition, dataStream);
                }
                else if (updatedPartition.DataLength < MinPartitionDataSize)
                {
                    var allPartitions = await partitioningStrategy.GetAllPartitionsAsync();
                    if (allPartitions.Count(p => p is DataPartition dp && dp.GetPartitionKey().LogicalKey.Equals(logicalKey)) > 1)
                    {
                        await MergePartitionIfNeededAsync(updatedPartition, allPartitions, dataStream);
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetPartitionAsync(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key is not CompositePartitionKey compositeKey)
        {
            throw new ArgumentException($"Key must be of type {nameof(CompositePartitionKey)} for partitioned documents.", nameof(key));
        }
        return await partitioningStrategy.FindPartitionAsync(compositeKey);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetPartitionContentAsync(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key is not CompositePartitionKey compositeKey)
        {
            throw new ArgumentException($"Key must be of type {nameof(CompositePartitionKey)} for partitioned documents.", nameof(key));
        }

        var partition = await partitioningStrategy.FindPartitionAsync(compositeKey);
        if (partition is null)
        {
            return null;
        }

        var dataStream = await streamProvider.GetDataStreamAsync(compositeKey.LogicalKey);
        
        // If it's a data partition, we need to assemble it with the header.
        if (partition is DataPartition)
        {
            // 1. Load the data partition content
            var dataDoc = await LoadPartitionContentAsync(partition, dataStream);

            // 2. Find and load the header partition content
            var headerKey = new CompositePartitionKey(compositeKey.LogicalKey, null);
            var headerPartition = await partitioningStrategy.FindPartitionAsync(headerKey) 
                ?? throw new InvalidOperationException($"Could not find header partition for logical key '{compositeKey.LogicalKey}'.");
            var headerDoc = await LoadPartitionContentAsync(headerPartition, dataStream);

            // 3. Merge data
            // The header document has the correct non-collection properties.
            // The data document has the correct collection property.
            var collection = partitionableProperty.GetValue(dataDoc.Data);
            partitionableProperty.SetValue(headerDoc.Data, collection);
            var mergedData = headerDoc.Data;

            // 4. Merge metadata
            var mergedMeta = metadataManager.Merge(headerDoc.Metadata!, dataDoc.Metadata!);

            return new CrdtDocument<T>(mergedData, mergedMeta);
        }
        
        // For header partitions or any other type, return its content directly.
        return await LoadPartitionContentAsync(partition, dataStream);
    }

    /// <inheritdoc/>
    public async Task<List<IPartition>> GetAllDataPartitionsAsync(object logicalKey)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        var allPartitions = await partitioningStrategy.GetAllPartitionsAsync();

        return allPartitions
            .OfType<DataPartition>()
            .Where(p => p.GetPartitionKey().LogicalKey.Equals(logicalKey))
            .OrderBy(p => p.StartKey)
            .Cast<IPartition>()
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<object>> GetAllLogicalKeysAsync()
    {
        await partitioningStrategy.InitializeAsync();
        var allPartitions = await partitioningStrategy.GetAllPartitionsAsync();
        return allPartitions
            .Select(p => p.GetPartitionKey().LogicalKey)
            .Distinct()
            .ToList();
    }

    private async Task<Dictionary<IPartition, List<CrdtOperation>>> GroupOperationsByPartitionAsync(CrdtPatch patch)
    {
        var opsByPartition = new Dictionary<IPartition, List<CrdtOperation>>();
        var logicalKey = patch.LogicalKey!;
        foreach (var op in patch.Operations!)
        {
            var rangeKey = partitionableStrategy.GetKeyFromOperation(op, partitionablePropertyPath);
            var compositeKey = new CompositePartitionKey(logicalKey, rangeKey);

            var partition = await partitioningStrategy.FindPartitionAsync(compositeKey)
                ?? throw new InvalidOperationException($"Could not find partition for key '{compositeKey}'.");

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

    private async Task SplitPartitionAsync(IPartition partitionToSplit, Stream dataStream)
    {
        if (partitionToSplit is not DataPartition dataPartitionToSplit) return;

        var crdtDoc = await LoadPartitionContentAsync(dataPartitionToSplit, dataStream);
        var splitResult = partitionableStrategy.Split(crdtDoc.Data!, crdtDoc.Metadata!, typeof(T));

        var p1DataWrite = await WriteToStreamAsync(dataStream, splitResult.Partition1.Data);
        var p1MetaWrite = await WriteToStreamAsync(dataStream, splitResult.Partition1.Metadata);
        var p2DataWrite = await WriteToStreamAsync(dataStream, splitResult.Partition2.Data);
        var p2MetaWrite = await WriteToStreamAsync(dataStream, splitResult.Partition2.Metadata);
        
        var originalKey = dataPartitionToSplit.StartKey;

        var p1Key = originalKey;
        var p2Key = new CompositePartitionKey(originalKey.LogicalKey, splitResult.SplitKey);

        var p1 = new DataPartition(p1Key, p2Key, p1DataWrite.Offset, p1DataWrite.Length, p1MetaWrite.Offset, p1MetaWrite.Length);
        var p2 = new DataPartition(p2Key, dataPartitionToSplit.EndKey, p2DataWrite.Offset, p2DataWrite.Length, p2MetaWrite.Offset, p2MetaWrite.Length);
        
        await partitioningStrategy.DeletePartitionAsync(dataPartitionToSplit);
        await partitioningStrategy.InsertPartitionAsync(p1);
        await partitioningStrategy.InsertPartitionAsync(p2);
    }

    private async Task MergePartitionIfNeededAsync(IPartition partitionToMerge, List<IPartition> allPartitions, Stream dataStream)
    {
        if (partitionToMerge is not DataPartition dataPartitionToMerge) return;

        var logicalKey = dataPartitionToMerge.StartKey.LogicalKey;
        var logicalPartitions = allPartitions
            .OfType<DataPartition>()
            .Where(p => p.StartKey.LogicalKey.Equals(logicalKey))
            .OrderBy(p => p.StartKey)
            .ToList();

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
        
        var targetDocument = await LoadPartitionContentAsync(targetPartition, dataStream);
        var sourceDocument = await LoadPartitionContentAsync(sourcePartition, dataStream);
        
        var mergedContent = partitionableStrategy.Merge(targetDocument.Data!, targetDocument.Metadata!, sourceDocument.Data!, sourceDocument.Metadata!, typeof(T));
        
        var dataWriteResult = await WriteToStreamAsync(dataStream, mergedContent.Data);
        var metaWriteResult = await WriteToStreamAsync(dataStream, mergedContent.Metadata);
        
        var mergedPartition = new DataPartition(targetPartition.StartKey, sourcePartition.EndKey, dataWriteResult.Offset, dataWriteResult.Length, metaWriteResult.Offset, metaWriteResult.Length);

        await partitioningStrategy.DeletePartitionAsync(targetPartition);
        await partitioningStrategy.DeletePartitionAsync(sourcePartition);
        await partitioningStrategy.InsertPartitionAsync(mergedPartition);
    }

    private static (PropertyInfo Property, IPartitionableCrdtStrategy Strategy) FindPartitionablePropertyAndStrategy(ICrdtStrategyProvider strategyProvider)
    {
        var partitionableProperties = typeof(T).GetProperties()
            .Select(p => new { Property = p, Strategy = strategyProvider.GetStrategy(p) })
            .Where(x => x.Strategy is IPartitionableCrdtStrategy)
            .ToList();

        if (partitionableProperties.Count == 0)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' does not have a property with a CRDT strategy that supports partitioning (implements {nameof(IPartitionableCrdtStrategy)}).");
        }
        if (partitionableProperties.Count > 1)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' has multiple properties with partitionable CRDT strategies. Only one is allowed per type.");
        }

        var result = partitionableProperties.First();
        return (result.Property, (IPartitionableCrdtStrategy)result.Strategy);
    }
    
    private static PropertyInfo FindPartitionKeyProperty()
    {
        var attr = typeof(T).GetCustomAttribute<PartitionKeyAttribute>();
        if (attr is null)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' must be decorated with the [{nameof(PartitionKeyAttribute)}] to be used with partitioning.");
        }
        
        var property = typeof(T).GetProperty(attr.PropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
        {
            throw new NotSupportedException($"The partition key property '{attr.PropertyName}' specified on type '{typeof(T).Name}' was not found.");
        }
        
        return property;
    }

    private static Type GetPartitionKeyType(Type propertyType)
    {
        // First, check if the type itself is a generic dictionary or enumerable interface
        if (propertyType.IsGenericType)
        {
            var genericDef = propertyType.GetGenericTypeDefinition();
            if (genericDef == typeof(IDictionary<,>))
            {
                return propertyType.GetGenericArguments()[0];
            }
            if (genericDef == typeof(IEnumerable<>))
            {
                return propertyType.GetGenericArguments()[0];
            }
        }

        // Then check implemented interfaces
        var dictionaryInterface = propertyType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        if (dictionaryInterface != null)
        {
            return dictionaryInterface.GetGenericArguments()[0];
        }

        var enumerableInterface = propertyType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerableInterface != null)
        {
            return enumerableInterface.GetGenericArguments()[0];
        }

        throw new NotSupportedException($"Cannot determine partition key type for property type '{propertyType.Name}'.");
    }

    private static object GetMinValueForType(Type type)
    {
        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type == typeof(Guid))
        {
            return Guid.Empty;
        }

        if (type.IsValueType)
        {
            var minValueField = type.GetField("MinValue", BindingFlags.Public | BindingFlags.Static);
            if (minValueField != null)
            {
                return minValueField.GetValue(null)!;
            }
        }

        throw new NotSupportedException($"Cannot determine a minimum value for partition key type '{type.Name}'. " +
                                        "This is required when initializing with an empty partitionable collection. " +
                                        "Either provide a non-empty collection on initialization or use a key type with a known minimum value " +
                                        "(e.g., int, long, string, DateTime, Guid).");
    }

    private async Task<CrdtDocument<T>> LoadPartitionContentAsync(IPartition partition, Stream dataStream)
    {
        var docBuffer = new byte[partition.DataLength];
        dataStream.Seek(partition.DataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(docBuffer);
        using var docStream = new MemoryStream(docBuffer);
        var doc = await JsonSerializer.DeserializeAsync<T>(docStream, CrdtJsonContext.DefaultOptions);
        
        var metaBuffer = new byte[partition.MetadataLength];
        dataStream.Seek(partition.MetadataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(metaBuffer);
        using var metaStream = new MemoryStream(metaBuffer);
        var meta = await JsonSerializer.DeserializeAsync<CrdtMetadata>(metaStream, CrdtJsonContext.DefaultOptions);

        return new CrdtDocument<T>(doc, meta);
    }
    
    private async Task<StreamWriteResult> WriteToStreamAsync(Stream dataStream, object content)
    {
        dataStream.Seek(0, SeekOrigin.End);
        var offset = dataStream.Position;
        await JsonSerializer.SerializeAsync(dataStream, content, content.GetType(), CrdtJsonContext.DefaultOptions);
        await dataStream.FlushAsync();
        var length = dataStream.Position - offset;
        return new StreamWriteResult(offset, length);
    }
    
    private async Task<IPartition> PersistPartitionChangesAsync(IPartition partitionToUpdate, T newData, CrdtMetadata newMeta, Stream dataStream)
    {
        var newDataWriteResult = await WriteToStreamAsync(dataStream, newData);
        var newMetaWriteResult = await WriteToStreamAsync(dataStream, newMeta);

        IPartition updatedPartition = partitionToUpdate switch
        {
            HeaderPartition hp => hp with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length },
            DataPartition dp => dp with { DataOffset = newDataWriteResult.Offset, DataLength = newDataWriteResult.Length, MetadataOffset = newMetaWriteResult.Offset, MetadataLength = newMetaWriteResult.Length },
            _ => throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}")
        };
        
        await partitioningStrategy.UpdatePartitionAsync(updatedPartition);

        return updatedPartition;
    }
}