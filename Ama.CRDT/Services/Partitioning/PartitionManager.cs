namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Manages querying and initialization of a CRDT document that is partitioned, allowing it to scale beyond available memory by storing data and an index in streams.
/// It uses a user-friendly API with property names and translates them to internal property paths for strategy execution.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class PartitionManager<T> : IPartitionManager<T> where T : class, new()
{
    public const int MaxPartitionDataSize = 8192;
    public const int MinPartitionDataSize = MaxPartitionDataSize / 4;

    private readonly IPartitionStorageService storageService;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly PartitionManagerCrdtMetrics metrics;
    private readonly IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories;
    private readonly IEnumerable<CrdtAotContext> aotContexts;

    private readonly CrdtPropertyInfo partitionKeyProperty;
    private readonly IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IPartitionableCrdtStrategy Strategy)> partitionableProperties;
    private readonly IReadOnlyDictionary<string, string> propertyNamePathCache;

    public PartitionManager(
        IPartitionStorageService storageService,
        ICrdtMetadataManager metadataManager,
        ICrdtStrategyProvider strategyProvider,
        ReplicaContext replicaContext,
        PartitionManagerCrdtMetrics metrics,
        IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories,
        IEnumerable<CrdtAotContext> aotContexts)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(metadataManager);
        ArgumentNullException.ThrowIfNull(strategyProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(compactionPolicyFactories);
        ArgumentNullException.ThrowIfNull(aotContexts);

        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException($"The service '{nameof(PartitionManager<T>)}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}.");
        }

        this.storageService = storageService;
        this.metadataManager = metadataManager;
        this.metrics = metrics;
        this.compactionPolicyFactories = compactionPolicyFactories;
        this.aotContexts = aotContexts;

        partitionKeyProperty = FindPartitionKeyProperty(typeof(T), aotContexts);
        partitionableProperties = FindPartitionablePropertiesAndStrategies(strategyProvider, aotContexts);
        propertyNamePathCache = partitionableProperties.ToDictionary(kvp => kvp.Value.Property.Name, kvp => kvp.Key);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(T initialObject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialObject);
        using var _ = new MetricTimer(metrics.InitializationDuration);

        var logicalKey = GetLogicalKey(initialObject);

        await InitializeHeaderAsync(logicalKey, initialObject, cancellationToken).ConfigureAwait(false);
        await InitializePropertiesAsync(logicalKey, initialObject, cancellationToken).ConfigureAwait(false);
    }
    
    /// <inheritdoc/>
    public async Task<T?> GetFullObjectAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetFullObjectDuration);

        var headerDoc = await GetHeaderPartitionContentAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        if (headerDoc is null)
        {
            return null;
        }

        var fullObject = headerDoc.Value.Data!;

        foreach (var (_, (prop, _)) in partitionableProperties)
        {
            var collection = prop.Getter!(fullObject);
            if (collection is not null)
            {
                PocoPathHelper.ClearCollection(collection, aotContexts);
            }

            await foreach(var partition in GetAllDataPartitionsAsync(logicalKey, prop.Name, cancellationToken).WithCancellation(cancellationToken))
            {
                var partitionDoc = await storageService.LoadPartitionContentAsync<T>(logicalKey, prop.Name, partition, cancellationToken).ConfigureAwait(false);

                var partitionCollection = prop.Getter!(partitionDoc.Data!);
                
                if (collection is not null && partitionCollection is IEnumerable penum)
                {
                    var typeInfo = PocoPathHelper.GetTypeInfo(collection.GetType(), aotContexts);
                    if (typeInfo.IsCollection && typeInfo.CollectionAdd != null)
                    {
                        foreach(var item in penum)
                        {
                            typeInfo.CollectionAdd(collection, item);
                        }
                    }
                    else if (typeInfo.IsDictionary && collection is IDictionary dict && partitionCollection is IDictionary pdict)
                    {
                        foreach (DictionaryEntry item in pdict)
                        {
                            dict.Add(item.Key, item.Value);
                        }
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
        return await storageService.GetHeaderPartitionAsync(logicalKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetHeaderPartitionContentAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);

        var headerPartition = await GetHeaderPartitionAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        if (headerPartition is null)
        {
            return null;
        }

        return await storageService.LoadHeaderPartitionContentAsync<T>(logicalKey, (HeaderPartition)headerPartition, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        
        return await storageService.GetPropertyPartitionAsync(key, propertyName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetDataPartitionContentAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetPartitionContentDuration);
        
        var partition = await storageService.GetPropertyPartitionAsync(key, propertyName, cancellationToken).ConfigureAwait(false);

        if (partition is not DataPartition)
        {
            return null;
        }

        var dataDoc = await storageService.LoadPartitionContentAsync<T>(key.LogicalKey, propertyName, partition, cancellationToken).ConfigureAwait(false);

        var headerDoc = await GetHeaderPartitionContentAsync(key.LogicalKey, cancellationToken).ConfigureAwait(false);
        if (headerDoc is null)
        {
            throw new InvalidOperationException($"Could not find header partition for logical key '{key.LogicalKey}'.");
        }

        var propertyPath = ToPropertyPath(propertyName);
        var (prop, _) = partitionableProperties[propertyPath];

        // Attach the partition's data collection to the header document
        var collection = prop.Getter!(dataDoc.Data!);
        prop.Setter!(headerDoc.Value.Data!, collection);

        var mergedMeta = CrdtMetadata.Merge(headerDoc.Value.Metadata!, dataDoc.Metadata!);
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
        
        return await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IPartition?> GetDataPartitionByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetDataPartitionByIndexDuration);
        
        if (index < 0) return null;
        
        return await storageService.GetPropertyPartitionByIndexAsync(logicalKey, index, propertyName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default)
    {
        using var _ = new MetricTimer(metrics.GetAllLogicalKeysDuration);
        await storageService.InitializeHeaderIndexAsync(cancellationToken).ConfigureAwait(false);
        
        var logicalKeys = new HashSet<IComparable>();
        await foreach (var partition in storageService.GetAllHeaderPartitionsAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            logicalKeys.Add(partition.GetPartitionKey().LogicalKey);
        }
        return logicalKeys;
    }

    /// <inheritdoc/>
    public async Task CompactAsync(CancellationToken cancellationToken = default)
    {
        if (!compactionPolicyFactories.Any())
        {
            return;
        }

        var logicalKeys = await GetAllLogicalKeysAsync(cancellationToken).ConfigureAwait(false);

        foreach (var logicalKey in logicalKeys)
        {
            // Compact the Header Partition
            var headerPartition = await GetHeaderPartitionAsync(logicalKey, cancellationToken).ConfigureAwait(false);
            if (headerPartition is HeaderPartition hp)
            {
                var headerDoc = await storageService.LoadHeaderPartitionContentAsync<T>(logicalKey, hp, cancellationToken).ConfigureAwait(false);
                foreach (var factory in compactionPolicyFactories)
                {
                    var policy = factory.CreatePolicy();
                    metadataManager.Compact(headerDoc, policy);
                }

                var compactedHeader = await storageService.SaveHeaderPartitionContentAsync(logicalKey, hp, headerDoc.Data!, headerDoc.Metadata!, cancellationToken).ConfigureAwait(false);
                await storageService.InsertHeaderPartitionAsync(logicalKey, compactedHeader, cancellationToken).ConfigureAwait(false);
            }

            // Compact the Property Data Partitions
            foreach (var (_, (prop, _)) in partitionableProperties)
            {
                var partitionsToCompact = new List<DataPartition>();
                await foreach (var partition in GetAllDataPartitionsAsync(logicalKey, prop.Name, cancellationToken).WithCancellation(cancellationToken))
                {
                    if (partition is DataPartition dp)
                    {
                        partitionsToCompact.Add(dp);
                    }
                }

                foreach (var dataPartition in partitionsToCompact)
                {
                    var crdtDoc = await storageService.LoadPartitionContentAsync<T>(logicalKey, prop.Name, dataPartition, cancellationToken).ConfigureAwait(false);
                    
                    foreach (var factory in compactionPolicyFactories)
                    {
                        var policy = factory.CreatePolicy();
                        metadataManager.Compact(crdtDoc, policy);
                    }

                    var compactedPartition = await storageService.SavePartitionContentAsync(
                        logicalKey,
                        prop.Name,
                        dataPartition,
                        crdtDoc.Data!,
                        crdtDoc.Metadata!,
                        cancellationToken).ConfigureAwait(false);

                    await storageService.DeletePropertyPartitionAsync(prop.Name, dataPartition, cancellationToken).ConfigureAwait(false);
                    await storageService.InsertPropertyPartitionAsync(prop.Name, compactedPartition, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task InitializeHeaderAsync(IComparable logicalKey, T initialObject, CancellationToken cancellationToken)
    {
        var headerObject = new T();
        var typeInfo = PocoPathHelper.GetTypeInfo(typeof(T), aotContexts);
        var properties = typeInfo.Properties.Values.Where(p => p.CanRead && p.CanWrite);

        // Shallow copy all properties to the header
        foreach (var property in properties)
        {
            property.Setter!(headerObject, property.Getter!(initialObject));
        }

        // Isolate the header by providing empty instances of partitionable collections
        foreach (var kvp in partitionableProperties.Values)
        {
            var prop = kvp.Property;
            var originalCollection = prop.Getter!(initialObject);

            if (prop.CanWrite && originalCollection is not null)
            {
                var emptyCollection = PocoPathHelper.InstantiateCollection(prop.PropertyType, aotContexts);
                prop.Setter!(headerObject, emptyCollection);
            }
        }

        await storageService.ClearHeaderDataAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        await storageService.InitializeHeaderIndexAsync(cancellationToken).ConfigureAwait(false);

        var headerMetadata = metadataManager.Initialize(headerObject);
        var headerPartitionKey = new CompositePartitionKey(logicalKey, null);
        var headerPartition = new HeaderPartition(headerPartitionKey, 0, 0, 0, 0);

        headerPartition = await storageService.SaveHeaderPartitionContentAsync(logicalKey, headerPartition, headerObject, headerMetadata, cancellationToken).ConfigureAwait(false);
        await storageService.InsertHeaderPartitionAsync(logicalKey, headerPartition, cancellationToken).ConfigureAwait(false);
    }

    private async Task InitializePropertiesAsync(IComparable logicalKey, T initialObject, CancellationToken cancellationToken)
    {
        foreach (var (_, (prop, strategy)) in partitionableProperties)
        {
            await storageService.InitializePropertyIndexAsync(prop.Name, cancellationToken).ConfigureAwait(false);
            await storageService.ClearPropertyDataAsync(logicalKey, prop.Name, cancellationToken).ConfigureAwait(false);

            var initialCollection = prop.Getter!(initialObject);
            var dataObject = new T();
            partitionKeyProperty.Setter!(dataObject, logicalKey);
            prop.Setter!(dataObject, initialCollection);

            var dataMetadata = metadataManager.Initialize(dataObject);
            var startRangeKey = strategy.GetStartKey(initialObject, prop) ?? strategy.GetMinimumKey(prop);
            
            var dataPartitionKey = new CompositePartitionKey(logicalKey, startRangeKey);
            var dataPartition = new DataPartition(dataPartitionKey, null, 0, 0, 0, 0);

            dataPartition = (DataPartition)await storageService.SavePartitionContentAsync(logicalKey, prop.Name, dataPartition, dataObject, dataMetadata, cancellationToken).ConfigureAwait(false);
            await storageService.InsertPropertyPartitionAsync(prop.Name, dataPartition, cancellationToken).ConfigureAwait(false);

            if (dataPartition.DataLength > MaxPartitionDataSize)
            {
                await SplitPartitionAsync(dataPartition, prop.Name, strategy, prop, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SplitPartitionAsync(IPartition partitionToSplit, string propertyName, IPartitionableCrdtStrategy strategy, CrdtPropertyInfo prop, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(metrics.SplitPartitionDuration);
        if (partitionToSplit is not DataPartition dataPartitionToSplit) return;
        
        metrics.PartitionsSplit.Add(1);

        var crdtDoc = await storageService.LoadPartitionContentAsync<T>(dataPartitionToSplit.StartKey.LogicalKey, propertyName, dataPartitionToSplit, cancellationToken).ConfigureAwait(false);
        
        // 1. Attempt Piggybacked Compaction to avoid the split entirely
        if (compactionPolicyFactories.Any())
        {
            foreach (var factory in compactionPolicyFactories)
            {
                var policy = factory.CreatePolicy();
                metadataManager.Compact(crdtDoc, policy);
            }

            var compactedPartition = await storageService.SavePartitionContentAsync(
                dataPartitionToSplit.StartKey.LogicalKey,
                propertyName,
                dataPartitionToSplit,
                crdtDoc.Data!,
                crdtDoc.Metadata!,
                cancellationToken).ConfigureAwait(false);

            if (compactedPartition is DataPartition dp && dp.DataLength <= MaxPartitionDataSize)
            {
                await storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit, cancellationToken).ConfigureAwait(false);
                await storageService.InsertPropertyPartitionAsync(propertyName, dp, cancellationToken).ConfigureAwait(false);
                return;
            }

            dataPartitionToSplit = (DataPartition)compactedPartition;
        }

        // 2. Proceed with split
        SplitResult splitResult;
        using (new MetricTimer(metrics.StrategySplitDuration))
        {
            splitResult = strategy.Split(crdtDoc.Data!, crdtDoc.Metadata!, prop);
        }

        var originalKey = dataPartitionToSplit.StartKey;
        var p1Key = originalKey;
        var p2Key = new CompositePartitionKey(originalKey.LogicalKey, splitResult.SplitKey);

        var p1Empty = new DataPartition(p1Key, p2Key, 0, 0, 0, 0);
        var p2Empty = new DataPartition(p2Key, dataPartitionToSplit.EndKey, 0, 0, 0, 0);

        var p1 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p1Empty, (T)splitResult.Partition1.Data, splitResult.Partition1.Metadata, cancellationToken).ConfigureAwait(false);
        var p2 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p2Empty, (T)splitResult.Partition2.Data, splitResult.Partition2.Metadata, cancellationToken).ConfigureAwait(false);

        await storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit, cancellationToken).ConfigureAwait(false);
        await storageService.InsertPropertyPartitionAsync(propertyName, p1, cancellationToken).ConfigureAwait(false);
        await storageService.InsertPropertyPartitionAsync(propertyName, p2, cancellationToken).ConfigureAwait(false);

        if (p1 is DataPartition dp1 && dp1.DataLength > MaxPartitionDataSize && dp1.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync(dp1, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
        }

        if (p2 is DataPartition dp2 && dp2.DataLength > MaxPartitionDataSize && dp2.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync(dp2, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
        }
    }

    private IComparable GetLogicalKey(T obj)
    {
        var logicalKeyObj = partitionKeyProperty.Getter?.Invoke(obj) ?? throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' cannot be null.");
        if (logicalKeyObj is not IComparable logicalKey)
        {
            throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' must implement IComparable.");
        }
        return logicalKey;
    }
    
    private static IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IPartitionableCrdtStrategy Strategy)> FindPartitionablePropertiesAndStrategies(ICrdtStrategyProvider strategyProvider, IEnumerable<CrdtAotContext> aotContexts)
    {
        var typeInfo = PocoPathHelper.GetTypeInfo(typeof(T), aotContexts);

        var partitionableProperties = typeInfo.Properties.Values
            .Select(p => new
            {
                Property = p,
                Strategy = strategyProvider.GetStrategy(typeof(T), p),
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
    
    private static CrdtPropertyInfo FindPartitionKeyProperty(Type type, IEnumerable<CrdtAotContext> aotContexts)
    {
        var attr = type.GetCustomAttribute<PartitionKeyAttribute>();
        if (attr is null)
        {
            throw new NotSupportedException($"The type '{type.Name}' must be decorated with the [{nameof(PartitionKeyAttribute)}] to be used with partitioning.");
        }
        
        var typeInfo = PocoPathHelper.GetTypeInfo(type, aotContexts);
        if (!typeInfo.Properties.TryGetValue(attr.PropertyName, out var property))
        {
            throw new NotSupportedException($"The partition key property '{attr.PropertyName}' specified on type '{type.Name}' was not found.");
        }
        
        return property;
    }

    private string ToPropertyPath(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (!propertyNamePathCache.TryGetValue(propertyName, out var propertyPath))
        {
            throw new ArgumentException($"Property '{propertyName}' is not a partitionable property on type '{typeof(T).Name}'.", nameof(propertyName));
        }
        return propertyPath;
    }
}