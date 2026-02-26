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
using System.Runtime.CompilerServices;
using System.Threading;
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
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(crdtApplicator);
        ArgumentNullException.ThrowIfNull(metadataManager);
        ArgumentNullException.ThrowIfNull(strategyProvider);
        ArgumentNullException.ThrowIfNull(metrics);

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
    public async Task InitializeAsync(T initialObject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialObject);
        using var _ = new MetricTimer(metrics.InitializationDuration);

        var logicalKey = GetLogicalKey(initialObject);

        await InitializeHeaderAsync(logicalKey, initialObject, cancellationToken);
        await InitializePropertiesAsync(logicalKey, initialObject, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ApplyPatchAsync(CrdtPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
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
            await ApplyToHeaderPartitionAsync(patch.LogicalKey, headerOps, cancellationToken);
        }

        foreach (var (propertyName, operations) in propertyOps)
        {
            await ApplyToDataPartitionsAsync(patch.LogicalKey, propertyName, operations, cancellationToken);
        }
    }
    
    /// <inheritdoc/>
    public async Task<T?> GetFullObjectAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetFullObjectDuration);

        var headerDoc = await GetHeaderPartitionContentAsync(logicalKey, cancellationToken);
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

            await foreach(var partition in GetAllDataPartitionsAsync(logicalKey, prop.Name, cancellationToken).WithCancellation(cancellationToken))
            {
                var partitionDoc = await GetDataPartitionContentAsync(partition.GetPartitionKey(), prop.Name, cancellationToken);
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
    public async Task<IPartition?> GetHeaderPartitionAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        return await storageService.GetHeaderPartitionAsync(logicalKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetHeaderPartitionContentAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);

        var headerPartition = await GetHeaderPartitionAsync(logicalKey, cancellationToken);
        if (headerPartition is null)
        {
            return null;
        }

        return await storageService.LoadHeaderPartitionContentAsync<T>(logicalKey, (HeaderPartition)headerPartition, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        
        return await storageService.GetPropertyPartitionAsync(key, propertyName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetDataPartitionContentAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);
        
        var partition = await storageService.GetPropertyPartitionAsync(key, propertyName, cancellationToken);

        if (partition is not DataPartition)
        {
            return null;
        }

        var dataDoc = await storageService.LoadPartitionContentAsync<T>(key.LogicalKey, propertyName, partition, cancellationToken);

        var headerDoc = await GetHeaderPartitionContentAsync(key.LogicalKey, cancellationToken);
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
    public async IAsyncEnumerable<IPartition> GetAllDataPartitionsAsync(IComparable logicalKey, string propertyName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetAllDataPartitionsDuration);
        
        await foreach (var partition in storageService.GetPartitionsAsync(logicalKey, propertyName, cancellationToken).WithCancellation(cancellationToken))
        {
            if (partition is DataPartition dataPartition)
            {
                yield return dataPartition;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetDataPartitionCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetDataPartitionCountDuration);
        
        return await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetDataPartitionByIndexDuration);
        
        if (index < 0) return null;
        
        return await storageService.GetPropertyPartitionByIndexAsync(logicalKey, index, propertyName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default)
    {
        using var _ = new MetricTimer(metrics.GetAllLogicalKeysDuration);
        await storageService.InitializeHeaderIndexAsync(cancellationToken);
        
        var logicalKeys = new HashSet<IComparable>();
        await foreach (var partition in storageService.GetAllHeaderPartitionsAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            logicalKeys.Add(partition.GetPartitionKey().LogicalKey);
        }
        return logicalKeys;
    }

    private async Task InitializeHeaderAsync(IComparable logicalKey, T initialObject, CancellationToken cancellationToken)
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

        await storageService.ClearHeaderDataAsync(logicalKey, cancellationToken);
        await storageService.InitializeHeaderIndexAsync(cancellationToken);

        var headerMetadata = metadataManager.Initialize(headerObject);
        var headerPartitionKey = new CompositePartitionKey(logicalKey, null);
        var headerPartition = new HeaderPartition(headerPartitionKey, 0, 0, 0, 0);

        headerPartition = await storageService.SaveHeaderPartitionContentAsync(logicalKey, headerPartition, headerObject, headerMetadata, cancellationToken);
        await storageService.InsertHeaderPartitionAsync(logicalKey, headerPartition, cancellationToken);
    }

    private async Task InitializePropertiesAsync(IComparable logicalKey, T initialObject, CancellationToken cancellationToken)
    {
        foreach (var (_, (prop, strategy)) in partitionableProperties)
        {
            await storageService.InitializePropertyIndexAsync(prop.Name, cancellationToken);
            await storageService.ClearPropertyDataAsync(logicalKey, prop.Name, cancellationToken);

            var initialCollection = prop.GetValue(initialObject);
            var dataObject = new T();
            partitionKeyProperty.SetValue(dataObject, logicalKey);
            prop.SetValue(dataObject, initialCollection);

            var dataMetadata = metadataManager.Initialize(dataObject);
            var startRangeKey = strategy.GetStartKey(initialObject, prop) ?? strategy.GetMinimumKey(prop);
            
            var dataPartitionKey = new CompositePartitionKey(logicalKey, startRangeKey);
            var dataPartition = new DataPartition(dataPartitionKey, null, 0, 0, 0, 0);

            dataPartition = (DataPartition)await storageService.SavePartitionContentAsync(logicalKey, prop.Name, dataPartition, dataObject, dataMetadata, cancellationToken);
            await storageService.InsertPropertyPartitionAsync(prop.Name, dataPartition, cancellationToken);

            if (dataPartition.DataLength > MaxPartitionDataSize)
            {
                await SplitPartitionAsync(dataPartition, prop.Name, strategy, prop, cancellationToken);
            }
        }
    }
    
    private async Task ApplyToHeaderPartitionAsync(IComparable logicalKey, IEnumerable<CrdtOperation> operations, CancellationToken cancellationToken)
    {
        var headerDoc = await GetHeaderPartitionContentAsync(logicalKey, cancellationToken)
            ?? throw new InvalidOperationException($"Could not find header partition for logical key '{logicalKey}'.");
        
        var headerPartition = await GetHeaderPartitionAsync(logicalKey, cancellationToken);
        
        crdtApplicator.ApplyPatch(headerDoc, new CrdtPatch(operations.ToList().AsReadOnly()) { LogicalKey = logicalKey });
        
        await PersistPartitionChangesAsync(logicalKey, headerPartition!, headerDoc.Data!, headerDoc.Metadata!, null, cancellationToken);
    }
    
    private async Task ApplyToDataPartitionsAsync(IComparable logicalKey, string propertyName, IEnumerable<CrdtOperation> operations, CancellationToken cancellationToken)
    {
        var propertyPath = ToPropertyPath(propertyName);
        var (prop, strategy) = partitionableProperties[propertyPath];

        var opsByPartition = await GroupOperationsByPartitionAsync(logicalKey, propertyName, strategy, prop, operations, cancellationToken);
        
        foreach(var (partition, ops) in opsByPartition)
        {
            var crdtDoc = await GetDataPartitionContentAsync(partition.GetPartitionKey(), prop.Name, cancellationToken);
            if (crdtDoc is null) continue;

            using (new MetricTimer(metrics.ApplicatorApplyPatchDuration))
            {
                crdtApplicator.ApplyPatch(crdtDoc.Value, new CrdtPatch(ops) { LogicalKey = logicalKey });
            }
            
            var updatedPartition = await PersistPartitionChangesAsync(logicalKey, partition, crdtDoc.Value.Data!, crdtDoc.Value.Metadata!, propertyName, cancellationToken);
            
            if (updatedPartition is DataPartition updatedDataPartition)
            {
                if (updatedDataPartition.DataLength > MaxPartitionDataSize)
                {
                    await SplitPartitionAsync(updatedDataPartition, propertyName, strategy, prop, cancellationToken);
                }
                else if (updatedDataPartition.DataLength < MinPartitionDataSize)
                {
                    var partitionCount = await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken);
                    if (partitionCount > 1)
                    {
                        await MergePartitionIfNeededAsync(updatedDataPartition, propertyName, strategy, prop, cancellationToken);
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

    private async Task<Dictionary<IPartition, List<CrdtOperation>>> GroupOperationsByPartitionAsync(IComparable logicalKey, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, IEnumerable<CrdtOperation> operations, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(metrics.GroupOperationsDuration);
        var opsByPartition = new Dictionary<IPartition, List<CrdtOperation>>();
        
        var propertyPath = ToPropertyPath(propertyName);

        foreach (var op in operations)
        {
            var rangeKey = strategy.GetKeyFromOperation(op, propertyPath);
            var compositeKey = new CompositePartitionKey(logicalKey, rangeKey);

            var partition = await storageService.GetPropertyPartitionAsync(compositeKey, propertyName, cancellationToken)
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

    private async Task SplitPartitionAsync(IPartition partitionToSplit, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(metrics.SplitPartitionDuration);
        if (partitionToSplit is not DataPartition dataPartitionToSplit) return;
        
        metrics.PartitionsSplit.Add(1);

        var crdtDoc = await GetDataPartitionContentAsync(dataPartitionToSplit.GetPartitionKey(), prop.Name, cancellationToken);
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

        var p1 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p1Empty, (T)splitResult.Partition1.Data, splitResult.Partition1.Metadata, cancellationToken);
        var p2 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p2Empty, (T)splitResult.Partition2.Data, splitResult.Partition2.Metadata, cancellationToken);

        await storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit, cancellationToken);
        await storageService.InsertPropertyPartitionAsync(propertyName, p1, cancellationToken);
        await storageService.InsertPropertyPartitionAsync(propertyName, p2, cancellationToken);
    }

    private async Task MergePartitionIfNeededAsync(IPartition partitionToMerge, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(metrics.MergePartitionDuration);
        if (partitionToMerge is not DataPartition dataPartitionToMerge) return;

        var logicalKey = dataPartitionToMerge.StartKey.LogicalKey;
        
        DataPartition targetPartition;
        DataPartition sourcePartition;

        if (dataPartitionToMerge.EndKey.HasValue)
        {
            // Prefer merging with the next partition to avoid scanning all partitions.
            // Since EndKey equals the StartKey of the next partition, this is a highly optimized O(1) lookup.
            var nextPartitionObj = await storageService.GetPropertyPartitionAsync(dataPartitionToMerge.EndKey.Value, propertyName, cancellationToken);
            if (nextPartitionObj is not DataPartition nextPartition) return;

            targetPartition = dataPartitionToMerge;
            sourcePartition = nextPartition;
        }
        else
        {
            // If EndKey is null, it is the last partition, so it has no next partition. 
            // Merge with the previous one. We retrieve it efficiently via index.
            var partitionCount = await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken);
            if (partitionCount < 2) return;

            // The last partition is at index `partitionCount - 1`, so the previous is at `partitionCount - 2`.
            var previousPartitionObj = await storageService.GetPropertyPartitionByIndexAsync(logicalKey, partitionCount - 2, propertyName, cancellationToken);
            if (previousPartitionObj is not DataPartition previousPartition) return;

            targetPartition = previousPartition;
            sourcePartition = dataPartitionToMerge;
        }
        
        var targetDocument = await GetDataPartitionContentAsync(targetPartition.GetPartitionKey(), prop.Name, cancellationToken);
        var sourceDocument = await GetDataPartitionContentAsync(sourcePartition.GetPartitionKey(), prop.Name, cancellationToken);
        if (targetDocument is null || sourceDocument is null) return;
        
        var mergedContent = strategy.Merge(targetDocument.Value.Data!, targetDocument.Value.Metadata!, sourceDocument.Value.Data!, sourceDocument.Value.Metadata!, prop);
        var mergedEmpty = new DataPartition(targetPartition.StartKey, sourcePartition.EndKey, 0, 0, 0, 0);
        var mergedPartition = await storageService.SavePartitionContentAsync(logicalKey, propertyName, mergedEmpty, (T)mergedContent.Data, mergedContent.Metadata, cancellationToken);

        await storageService.DeletePropertyPartitionAsync(propertyName, targetPartition, cancellationToken);
        await storageService.DeletePropertyPartitionAsync(propertyName, sourcePartition, cancellationToken);
        await storageService.InsertPropertyPartitionAsync(propertyName, mergedPartition, cancellationToken);
        
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
    
    private async Task<IPartition> PersistPartitionChangesAsync(IComparable logicalKey, IPartition partitionToUpdate, T newData, CrdtMetadata newMeta, string? propertyName, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(metrics.PersistChangesDuration);

        if (partitionToUpdate is HeaderPartition hp)
        {
            var updatedHeader = await storageService.SaveHeaderPartitionContentAsync(logicalKey, hp, newData, newMeta, cancellationToken);
            await storageService.UpdateHeaderPartitionAsync(logicalKey, updatedHeader, cancellationToken);
            return updatedHeader;
        }
        else if (partitionToUpdate is DataPartition dp)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            var updatedData = await storageService.SavePartitionContentAsync(logicalKey, propertyName, dp, newData, newMeta, cancellationToken);
            await storageService.UpdatePropertyPartitionAsync(propertyName, updatedData, cancellationToken);
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