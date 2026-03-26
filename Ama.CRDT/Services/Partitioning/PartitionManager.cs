namespace Ama.CRDT.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.GarbageCollection;
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
/// Manages querying and initialization of a CRDT document that is partitioned, allowing it to scale beyond available memory by storing data and an index in streams.
/// It uses a user-friendly API with property names and translates them to internal property paths for strategy execution.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class PartitionManager<T> : IPartitionManager<T> where T : class, new()
{
    public const int MaxPartitionDataSize = 8192;
    public const int MinPartitionDataSize = MaxPartitionDataSize / 4;

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)>> partitionablePropertyCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> partitionKeyCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, string>> propertyNamePathCache = new();

    private readonly IPartitionStorageService storageService;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly PartitionManagerCrdtMetrics metrics;
    private readonly IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories;

    private readonly PropertyInfo partitionKeyProperty;
    private readonly IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)> partitionableProperties;

    public PartitionManager(
        IPartitionStorageService storageService,
        ICrdtMetadataManager metadataManager,
        ICrdtStrategyProvider strategyProvider,
        ReplicaContext replicaContext,
        PartitionManagerCrdtMetrics metrics,
        IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(metadataManager);
        ArgumentNullException.ThrowIfNull(strategyProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(compactionPolicyFactories);

        if (replicaContext == null || string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            throw new InvalidOperationException($"The service '{nameof(PartitionManager<T>)}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}.");
        }

        this.storageService = storageService;
        this.metadataManager = metadataManager;
        this.metrics = metrics;
        this.compactionPolicyFactories = compactionPolicyFactories;

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
            var collection = PocoPathHelper.GetAccessor(prop).Getter(fullObject);
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
                var partitionDoc = await storageService.LoadPartitionContentAsync<T>(logicalKey, prop.Name, partition, cancellationToken);

                var partitionCollection = PocoPathHelper.GetAccessor(prop).Getter(partitionDoc.Data!);
                
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
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var _ = new MetricTimer(metrics.GetPartitionDuration);
        
        return await storageService.GetPropertyPartitionAsync(key, propertyName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetDataPartitionContentAsync(CompositePartitionKey key, string propertyName, CancellationToken cancellationToken = default)
    {
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
        var collection = PocoPathHelper.GetAccessor(prop).Getter(dataDoc.Data!);
        PocoPathHelper.GetAccessor(prop).Setter(headerDoc.Value.Data!, collection);

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

    /// <inheritdoc/>
    public async Task CompactAsync(CancellationToken cancellationToken = default)
    {
        if (!compactionPolicyFactories.Any())
        {
            return;
        }

        var logicalKeys = await GetAllLogicalKeysAsync(cancellationToken);

        foreach (var logicalKey in logicalKeys)
        {
            // Compact the Header Partition
            var headerPartition = await GetHeaderPartitionAsync(logicalKey, cancellationToken);
            if (headerPartition is HeaderPartition hp)
            {
                var headerDoc = await storageService.LoadHeaderPartitionContentAsync<T>(logicalKey, hp, cancellationToken);
                foreach (var factory in compactionPolicyFactories)
                {
                    var policy = factory.CreatePolicy();
                    metadataManager.Compact(headerDoc, policy);
                }

                var compactedHeader = await storageService.SaveHeaderPartitionContentAsync(logicalKey, hp, headerDoc.Data!, headerDoc.Metadata!, cancellationToken);
                await storageService.InsertHeaderPartitionAsync(logicalKey, compactedHeader, cancellationToken);
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
                    var crdtDoc = await storageService.LoadPartitionContentAsync<T>(logicalKey, prop.Name, dataPartition, cancellationToken);
                    
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
                        cancellationToken);

                    await storageService.DeletePropertyPartitionAsync(prop.Name, dataPartition, cancellationToken);
                    await storageService.InsertPropertyPartitionAsync(prop.Name, compactedPartition, cancellationToken);
                }
            }
        }
    }

    private async Task InitializeHeaderAsync(IComparable logicalKey, T initialObject, CancellationToken cancellationToken)
    {
        var headerObject = new T();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite);

        // Shallow copy all properties to the header
        foreach (var property in properties)
        {
            PocoPathHelper.GetAccessor(property).Setter(headerObject, PocoPathHelper.GetAccessor(property).Getter(initialObject));
        }

        // Isolate the header by providing empty instances of partitionable collections
        foreach (var kvp in partitionableProperties.Values)
        {
            var prop = kvp.Property;
            var originalCollection = PocoPathHelper.GetAccessor(prop).Getter(initialObject);

            if (prop.CanWrite && originalCollection is not null)
            {
                var emptyCollection = Activator.CreateInstance(originalCollection.GetType());
                PocoPathHelper.GetAccessor(prop).Setter(headerObject, emptyCollection);
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

            var initialCollection = PocoPathHelper.GetAccessor(prop).Getter(initialObject);
            var dataObject = new T();
            PocoPathHelper.GetAccessor(partitionKeyProperty).Setter(dataObject, logicalKey);
            PocoPathHelper.GetAccessor(prop).Setter(dataObject, initialCollection);

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

    private async Task SplitPartitionAsync(IPartition partitionToSplit, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(metrics.SplitPartitionDuration);
        if (partitionToSplit is not DataPartition dataPartitionToSplit) return;
        
        metrics.PartitionsSplit.Add(1);

        var crdtDoc = await storageService.LoadPartitionContentAsync<T>(dataPartitionToSplit.StartKey.LogicalKey, propertyName, dataPartitionToSplit, cancellationToken);
        
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
                cancellationToken);

            if (compactedPartition is DataPartition dp && dp.DataLength <= MaxPartitionDataSize)
            {
                await storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit, cancellationToken);
                await storageService.InsertPropertyPartitionAsync(propertyName, dp, cancellationToken);
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

        var p1 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p1Empty, (T)splitResult.Partition1.Data, splitResult.Partition1.Metadata, cancellationToken);
        var p2 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p2Empty, (T)splitResult.Partition2.Data, splitResult.Partition2.Metadata, cancellationToken);

        await storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit, cancellationToken);
        await storageService.InsertPropertyPartitionAsync(propertyName, p1, cancellationToken);
        await storageService.InsertPropertyPartitionAsync(propertyName, p2, cancellationToken);

        if (p1 is DataPartition dp1 && dp1.DataLength > MaxPartitionDataSize && dp1.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync(dp1, propertyName, strategy, prop, cancellationToken);
        }

        if (p2 is DataPartition dp2 && dp2.DataLength > MaxPartitionDataSize && dp2.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync(dp2, propertyName, strategy, prop, cancellationToken);
        }
    }

    private IComparable GetLogicalKey(T obj)
    {
        var logicalKeyObj = PocoPathHelper.GetAccessor(partitionKeyProperty).Getter(obj) ?? throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' cannot be null.");
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