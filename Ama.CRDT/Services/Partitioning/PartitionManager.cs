namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// Manages a CRDT document that is partitioned, allowing it to scale beyond available memory by storing data and an index in streams.
/// It uses a user-friendly API with property names and translates them to internal property paths for strategy execution.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class PartitionManager<T> : IPartitionManager<T> where T : class, new()
{
    private const int MaxPartitionDataSize = 8192;
    private const int MinPartitionDataSize = MaxPartitionDataSize / 4;

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)>> partitionablePropertyCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> partitionKeyCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, string>> propertyNamePathCache = new();

    private readonly IPartitionStorageService storageService;
    private readonly ICrdtApplicator crdtApplicator;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly PartitionManagerCrdtMetrics metrics;

    private readonly PropertyInfo partitionKeyProperty;
    private readonly IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)> partitionableProperties;

    public PartitionManager(
        IPartitionStorageService storageService,
        ICrdtApplicator crdtApplicator,
        ICrdtMetadataManager metadataManager,
        ICrdtStrategyProvider strategyProvider,
        ReplicaContext replicaContext,
        PartitionManagerCrdtMetrics metrics)
    {
        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException($"The service '{nameof(PartitionManager<T>)}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}.");
        }

        this.storageService = storageService;
        this.crdtApplicator = crdtApplicator;
        this.metadataManager = metadataManager;
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

        await InitializeHeaderAsync(logicalKey, initialObject);
        await InitializePropertiesAsync(logicalKey, initialObject);
    }

    /// <inheritdoc/>
    public async Task ApplyPatchAsync(CrdtPatch patch)
    {
        using var _ = new MetricTimer(metrics.ApplyPatchDuration);
        if (patch.Operations is null || !patch.Operations.Any())
        {
            return;
        }

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
        if (headerDoc is null)
        {
            return null;
        }

        var fullObject = headerDoc.Value.Data!;

        foreach (var (_, (prop, _)) in partitionableProperties)
        {
            var collection = prop.GetValue(fullObject);
            if (collection is IList list)
            {
                list.Clear();
            }
            else if (collection is IDictionary dict)
            {
                dict.Clear();
            }

            await foreach(var partition in GetAllDataPartitionsAsync(logicalKey, prop.Name))
            {
                var partitionDoc = await GetDataPartitionContentAsync(partition.GetPartitionKey(), prop.Name);
                if (partitionDoc is null) continue;

                var partitionCollection = prop.GetValue(partitionDoc.Value.Data!);
                
                if (collection is IList partitionList && partitionCollection is IEnumerable penum)
                {
                    foreach(var item in penum)
                    {
                        partitionList.Add(item);
                    }
                }
                else if (collection is IDictionary dict && partitionCollection is IDictionary pdict)
                {
                    foreach (DictionaryEntry item in pdict)
                    {
                        dict.Add(item.Key, item.Value);
                    }
                }
            }
        }

        return fullObject;
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetHeaderPartitionAsync(IComparable logicalKey)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        return await storageService.GetHeaderPartitionAsync(logicalKey);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetHeaderPartitionContentAsync(IComparable logicalKey)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);

        var headerPartition = await GetHeaderPartitionAsync(logicalKey);
        if (headerPartition is null)
        {
            return null;
        }

        return await storageService.LoadHeaderPartitionContentAsync<T>(logicalKey, (HeaderPartition)headerPartition);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionAsync(CompositePartitionKey key, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        return await storageService.GetPropertyPartitionAsync(key, propertyName);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetDataPartitionContentAsync(CompositePartitionKey key, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);
        var partition = await storageService.GetPropertyPartitionAsync(key, propertyName);

        if (partition is not DataPartition)
        {
            return null;
        }

        var dataDoc = await storageService.LoadPartitionContentAsync<T>(key.LogicalKey, propertyName, partition);

        var headerDoc = await GetHeaderPartitionContentAsync(key.LogicalKey);
        if (headerDoc is null)
        {
            throw new InvalidOperationException($"Could not find header partition for logical key '{key.LogicalKey}'.");
        }

        var propertyPath = ToPropertyPath(propertyName);
        var (prop, _) = partitionableProperties[propertyPath];

        // Attach the partition's data collection to the header document
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
        
        await foreach (var partition in storageService.GetPartitionsAsync(logicalKey, propertyName))
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
        return await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName)
    {
        using var _ = new MetricTimer(metrics.GetDataPartitionByIndexDuration);
        ArgumentNullException.ThrowIfNull(logicalKey);
        if (index < 0) return null;
        
        return await storageService.GetPropertyPartitionByIndexAsync(logicalKey, index, propertyName);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync()
    {
        using var _ = new MetricTimer(metrics.GetAllLogicalKeysDuration);
        await storageService.InitializeHeaderIndexAsync();
        
        var logicalKeys = new HashSet<IComparable>();
        await foreach (var partition in storageService.GetAllHeaderPartitionsAsync())
        {
            logicalKeys.Add(partition.GetPartitionKey().LogicalKey);
        }
        return logicalKeys;
    }

    private async Task InitializeHeaderAsync(IComparable logicalKey, T initialObject)
    {
        var headerObject = new T();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite);

        // Shallow copy all properties to the header
        foreach (var property in properties)
        {
            property.SetValue(headerObject, property.GetValue(initialObject));
        }

        // Isolate the header by providing empty instances of partitionable collections
        foreach (var kvp in partitionableProperties.Values)
        {
            var prop = kvp.Property;
            var originalCollection = prop.GetValue(initialObject);

            if (prop.CanWrite && originalCollection is not null)
            {
                var emptyCollection = Activator.CreateInstance(originalCollection.GetType());
                prop.SetValue(headerObject, emptyCollection);
            }
        }

        await storageService.ClearHeaderDataAsync(logicalKey);
        await storageService.InitializeHeaderIndexAsync();

        var headerMetadata = metadataManager.Initialize(headerObject);
        var headerPartitionKey = new CompositePartitionKey(logicalKey, null);
        var headerPartition = new HeaderPartition(headerPartitionKey, 0, 0, 0, 0);

        headerPartition = await storageService.SaveHeaderPartitionContentAsync(logicalKey, headerPartition, headerObject, headerMetadata);
        await storageService.InsertHeaderPartitionAsync(logicalKey, headerPartition);
    }

    private async Task InitializePropertiesAsync(IComparable logicalKey, T initialObject)
    {
        foreach (var (_, (prop, strategy)) in partitionableProperties)
        {
            await storageService.InitializePropertyIndexAsync(prop.Name);
            await storageService.ClearPropertyDataAsync(logicalKey, prop.Name);

            var initialCollection = prop.GetValue(initialObject);
            var dataObject = new T();
            partitionKeyProperty.SetValue(dataObject, logicalKey);
            prop.SetValue(dataObject, initialCollection);

            var dataMetadata = metadataManager.Initialize(dataObject);
            var startRangeKey = strategy.GetStartKey(initialObject, prop) ?? strategy.GetMinimumKey(prop);
            
            var dataPartitionKey = new CompositePartitionKey(logicalKey, startRangeKey);
            var dataPartition = new DataPartition(dataPartitionKey, null, 0, 0, 0, 0);

            dataPartition = (DataPartition)await storageService.SavePartitionContentAsync(logicalKey, prop.Name, dataPartition, dataObject, dataMetadata);
            await storageService.InsertPropertyPartitionAsync(prop.Name, dataPartition);

            if (dataPartition.DataLength > MaxPartitionDataSize)
            {
                await SplitPartitionAsync(dataPartition, prop.Name, strategy, prop);
            }
        }
    }
    
    private async Task ApplyToHeaderPartitionAsync(IComparable logicalKey, IEnumerable<CrdtOperation> operations)
    {
        var headerDoc = await GetHeaderPartitionContentAsync(logicalKey)
            ?? throw new InvalidOperationException($"Could not find header partition for logical key '{logicalKey}'.");
        
        var headerPartition = await GetHeaderPartitionAsync(logicalKey);
        
        crdtApplicator.ApplyPatch(headerDoc, new CrdtPatch(operations.ToList().AsReadOnly()) { LogicalKey = logicalKey });
        
        await PersistPartitionChangesAsync(logicalKey, headerPartition!, headerDoc.Data!, headerDoc.Metadata!, null);
    }
    
    private async Task ApplyToDataPartitionsAsync(IComparable logicalKey, string propertyName, IEnumerable<CrdtOperation> operations)
    {
        var propertyPath = ToPropertyPath(propertyName);
        var (prop, strategy) = partitionableProperties[propertyPath];

        var opsByPartition = await GroupOperationsByPartitionAsync(logicalKey, propertyName, strategy, prop, operations);
        
        foreach(var (partition, ops) in opsByPartition)
        {
            var crdtDoc = await GetDataPartitionContentAsync(partition.GetPartitionKey(), prop.Name);
            if (crdtDoc is null) continue;

            using (new MetricTimer(metrics.ApplicatorApplyPatchDuration))
            {
                crdtApplicator.ApplyPatch(crdtDoc.Value, new CrdtPatch(ops) { LogicalKey = logicalKey });
            }
            
            var updatedPartition = await PersistPartitionChangesAsync(logicalKey, partition, crdtDoc.Value.Data!, crdtDoc.Value.Metadata!, propertyName);
            
            if (updatedPartition is DataPartition updatedDataPartition)
            {
                if (updatedDataPartition.DataLength > MaxPartitionDataSize)
                {
                    await SplitPartitionAsync(updatedDataPartition, propertyName, strategy, prop);
                }
                else if (updatedDataPartition.DataLength < MinPartitionDataSize)
                {
                    var partitionCount = await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName);
                    if (partitionCount > 1)
                    {
                        await MergePartitionIfNeededAsync(updatedDataPartition, propertyName, strategy, prop);
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
                continue;
            }
            
            if (!propertyOps.TryGetValue(propertyName, out var list))
            {
                list = new List<CrdtOperation>();
                propertyOps[propertyName] = list;
            }
            
            list.Add(op);
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

            var partition = await storageService.GetPropertyPartitionAsync(compositeKey, propertyName)
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

    private async Task SplitPartitionAsync(IPartition partitionToSplit, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop)
    {
        using var _ = new MetricTimer(metrics.SplitPartitionDuration);
        if (partitionToSplit is not DataPartition dataPartitionToSplit) return;
        
        metrics.PartitionsSplit.Add(1);

        var crdtDoc = await GetDataPartitionContentAsync(dataPartitionToSplit.GetPartitionKey(), prop.Name);
        if (crdtDoc is null) return;
        
        SplitResult splitResult;
        using (new MetricTimer(metrics.StrategySplitDuration))
        {
            splitResult = strategy.Split(crdtDoc.Value.Data!, crdtDoc.Value.Metadata!, prop);
        }

        var originalKey = dataPartitionToSplit.StartKey;
        var p1Key = originalKey;
        var p2Key = new CompositePartitionKey(originalKey.LogicalKey, splitResult.SplitKey);

        var p1Empty = new DataPartition(p1Key, p2Key, 0, 0, 0, 0);
        var p2Empty = new DataPartition(p2Key, dataPartitionToSplit.EndKey, 0, 0, 0, 0);

        var p1 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p1Empty, (T)splitResult.Partition1.Data, splitResult.Partition1.Metadata);
        var p2 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p2Empty, (T)splitResult.Partition2.Data, splitResult.Partition2.Metadata);

        await storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit);
        await storageService.InsertPropertyPartitionAsync(propertyName, p1);
        await storageService.InsertPropertyPartitionAsync(propertyName, p2);
    }

    private async Task MergePartitionIfNeededAsync(IPartition partitionToMerge, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop)
    {
        using var _ = new MetricTimer(metrics.MergePartitionDuration);
        if (partitionToMerge is not DataPartition dataPartitionToMerge) return;

        var logicalKey = dataPartitionToMerge.StartKey.LogicalKey;
        var logicalPartitions = new List<DataPartition>();
        
        await foreach(var p in storageService.GetPartitionsAsync(logicalKey, propertyName))
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
        if (targetDocument is null || sourceDocument is null) return;
        
        var mergedContent = strategy.Merge(targetDocument.Value.Data!, targetDocument.Value.Metadata!, sourceDocument.Value.Data!, sourceDocument.Value.Metadata!, prop);
        var mergedEmpty = new DataPartition(targetPartition.StartKey, sourcePartition.EndKey, 0, 0, 0, 0);
        var mergedPartition = await storageService.SavePartitionContentAsync(logicalKey, propertyName, mergedEmpty, (T)mergedContent.Data, mergedContent.Metadata);

        await storageService.DeletePropertyPartitionAsync(propertyName, targetPartition);
        await storageService.DeletePropertyPartitionAsync(propertyName, sourcePartition);
        await storageService.InsertPropertyPartitionAsync(propertyName, mergedPartition);
        
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
    
    private async Task<IPartition> PersistPartitionChangesAsync(IComparable logicalKey, IPartition partitionToUpdate, T newData, CrdtMetadata newMeta, string? propertyName)
    {
        using var _ = new MetricTimer(metrics.PersistChangesDuration);

        if (partitionToUpdate is HeaderPartition hp)
        {
            var updatedHeader = await storageService.SaveHeaderPartitionContentAsync(logicalKey, hp, newData, newMeta);
            await storageService.UpdateHeaderPartitionAsync(logicalKey, updatedHeader);
            return updatedHeader;
        }
        else if (partitionToUpdate is DataPartition dp)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            var updatedData = await storageService.SavePartitionContentAsync(logicalKey, propertyName, dp, newData, newMeta);
            await storageService.UpdatePropertyPartitionAsync(propertyName, updatedData);
            return updatedData;
        }
        else
        {
            throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}");
        }
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