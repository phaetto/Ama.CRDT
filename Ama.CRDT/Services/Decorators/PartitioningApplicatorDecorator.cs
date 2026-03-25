namespace Ama.CRDT.Services.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A decorator that intercepts a patch application, slices it by partitionable properties,
/// loads the necessary partitions via the storage service, delegates the actual CRDT math
/// to the inner applicator, and saves the modified partitions back to storage.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class PartitioningApplicatorDecorator<T> : IAsyncCrdtApplicator where T : class, new()
{
    private const int MaxPartitionDataSize = 8192;
    private const int MinPartitionDataSize = MaxPartitionDataSize / 4;

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)>> partitionablePropertyCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> partitionKeyCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, string>> propertyNamePathCache = new();

    private readonly IAsyncCrdtApplicator _innerApplicator;
    private readonly IPartitionStorageService _storageService;
    private readonly PartitionManagerCrdtMetrics _metrics;

    private readonly PropertyInfo _partitionKeyProperty;
    private readonly IReadOnlyDictionary<string, (PropertyInfo Property, IPartitionableCrdtStrategy Strategy)> _partitionableProperties;

    public PartitioningApplicatorDecorator(
        IAsyncCrdtApplicator innerApplicator,
        IPartitionStorageService storageService,
        ICrdtStrategyProvider strategyProvider,
        PartitionManagerCrdtMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(innerApplicator);
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(strategyProvider);
        ArgumentNullException.ThrowIfNull(metrics);

        _innerApplicator = innerApplicator;
        _storageService = storageService;
        _metrics = metrics;

        _partitionKeyProperty = partitionKeyCache.GetOrAdd(typeof(T), FindPartitionKeyProperty);
        _partitionableProperties = partitionablePropertyCache.GetOrAdd(typeof(T), _ => FindPartitionablePropertiesAndStrategies(strategyProvider));
        propertyNamePathCache.GetOrAdd(typeof(T), _ => _partitionableProperties.ToDictionary(kvp => kvp.Value.Property.Name, kvp => kvp.Key));
    }

    /// <inheritdoc/>
    public async Task<ApplyPatchResult<TDoc>> ApplyPatchAsync<TDoc>([DisallowNull] CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken = default) where TDoc : class
    {
        using var _ = new MetricTimer(_metrics.ApplyPatchDuration);
        
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Data);

        if (document.Data is not T typedData)
        {
            throw new ArgumentException($"Document data must be of type {typeof(T).Name}");
        }

        var unappliedOperations = new List<UnappliedOperation>();

        if (patch.Operations is null || !patch.Operations.Any())
        {
            return new ApplyPatchResult<TDoc>(document.Data, unappliedOperations);
        }

        var logicalKey = GetLogicalKey(typedData);
        _metrics.PatchesApplied.Add(1);

        var headerPartition = await _storageService.GetHeaderPartitionAsync(logicalKey, cancellationToken)
            ?? throw new InvalidOperationException($"Could not find header partition for logical key '{logicalKey}'.");
        var headerDoc = await _storageService.LoadHeaderPartitionContentAsync<T>(logicalKey, (HeaderPartition)headerPartition, cancellationToken);

        var (headerOps, propertyOps) = GroupOperationsByProperty(patch.Operations);
        bool headerModified = headerOps.Count > 0;

        if (headerOps.Count > 0)
        {
            var headerResult = await _innerApplicator.ApplyPatchAsync(headerDoc, new CrdtPatch(headerOps.AsReadOnly()), cancellationToken);
            unappliedOperations.AddRange(headerResult.UnappliedOperations);
        }

        foreach (var (propertyName, operations) in propertyOps)
        {
            var propertyPath = ToPropertyPath(propertyName);
            var (prop, strategy) = _partitionableProperties[propertyPath];

            var opsByPartition = await GroupOperationsByPartitionAsync(logicalKey, propertyName, strategy, prop, operations, cancellationToken);
            
            foreach(var (partition, ops) in opsByPartition)
            {
                var dataDoc = await _storageService.LoadPartitionContentAsync<T>(logicalKey, propertyName, partition, cancellationToken);

                // Temporarily inject global synchronization state into the data partition's metadata 
                // so the inner CrdtApplicator can perform idempotency checks and advance the global clock.
                dataDoc.Metadata!.VersionVector = headerDoc.Metadata!.VersionVector;
                dataDoc.Metadata.SeenExceptions = headerDoc.Metadata.SeenExceptions;

                ApplyPatchResult<T> dataResult;
                using (new MetricTimer(_metrics.ApplicatorApplyPatchDuration))
                {
                    dataResult = await _innerApplicator.ApplyPatchAsync(dataDoc, new CrdtPatch(ops.AsReadOnly()), cancellationToken);
                }
                unappliedOperations.AddRange(dataResult.UnappliedOperations);

                // Remove global state from the data partition's metadata so it is not persisted in the data stream.
                dataDoc.Metadata.VersionVector = new Dictionary<string, long>();
                dataDoc.Metadata.SeenExceptions = new HashSet<CrdtOperation>();

                var updatedPartition = await PersistPartitionChangesAsync(logicalKey, partition, dataDoc.Data!, dataDoc.Metadata, propertyName, cancellationToken);
                
                if (updatedPartition is DataPartition updatedDataPartition)
                {
                    if (updatedDataPartition.DataLength > MaxPartitionDataSize)
                    {
                        await SplitPartitionAsync(updatedDataPartition, propertyName, strategy, prop, cancellationToken);
                    }
                    else if (updatedDataPartition.DataLength < MinPartitionDataSize)
                    {
                        var partitionCount = await _storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken);
                        if (partitionCount > 1)
                        {
                            await MergePartitionIfNeededAsync(updatedDataPartition, propertyName, strategy, prop, cancellationToken);
                        }
                    }
                }
                
                headerModified = true; // Data partition operations advance the global clock, requiring a header save.
            }
        }

        if (headerModified)
        {
            await PersistPartitionChangesAsync(logicalKey, headerPartition, headerDoc.Data!, headerDoc.Metadata!, null, cancellationToken);
        }
        
        return new ApplyPatchResult<TDoc>((headerDoc.Data as TDoc)!, unappliedOperations);
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

        return _partitionableProperties.TryGetValue(fullPath, out var val) ? val.Property.Name : null;
    }

    private async Task<Dictionary<IPartition, List<CrdtOperation>>> GroupOperationsByPartitionAsync(IComparable logicalKey, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, IEnumerable<CrdtOperation> operations, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(_metrics.GroupOperationsDuration);
        var opsByPartition = new Dictionary<IPartition, List<CrdtOperation>>();
        
        var propertyPath = ToPropertyPath(propertyName);

        foreach (var op in operations)
        {
            var rangeKey = strategy.GetKeyFromOperation(op, propertyPath);
            var compositeKey = new CompositePartitionKey(logicalKey, rangeKey);

            var partition = await _storageService.GetPropertyPartitionAsync(compositeKey, propertyName, cancellationToken)
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
        using var _ = new MetricTimer(_metrics.SplitPartitionDuration);
        if (partitionToSplit is not DataPartition dataPartitionToSplit) return;
        
        _metrics.PartitionsSplit.Add(1);

        var crdtDoc = await _storageService.LoadPartitionContentAsync<T>(dataPartitionToSplit.StartKey.LogicalKey, propertyName, dataPartitionToSplit, cancellationToken);
        
        SplitResult splitResult;
        using (new MetricTimer(_metrics.StrategySplitDuration))
        {
            splitResult = strategy.Split(crdtDoc.Data!, crdtDoc.Metadata!, prop);
        }

        var originalKey = dataPartitionToSplit.StartKey;
        var p1Key = originalKey;
        var p2Key = new CompositePartitionKey(originalKey.LogicalKey, splitResult.SplitKey);

        var p1Empty = new DataPartition(p1Key, p2Key, 0, 0, 0, 0);
        var p2Empty = new DataPartition(p2Key, dataPartitionToSplit.EndKey, 0, 0, 0, 0);

        var p1 = await _storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p1Empty, (T)splitResult.Partition1.Data, splitResult.Partition1.Metadata, cancellationToken);
        var p2 = await _storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p2Empty, (T)splitResult.Partition2.Data, splitResult.Partition2.Metadata, cancellationToken);

        await _storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit, cancellationToken);
        await _storageService.InsertPropertyPartitionAsync(propertyName, p1, cancellationToken);
        await _storageService.InsertPropertyPartitionAsync(propertyName, p2, cancellationToken);

        if (p1 is DataPartition dp1 && dp1.DataLength > MaxPartitionDataSize && dp1.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync(dp1, propertyName, strategy, prop, cancellationToken);
        }

        if (p2 is DataPartition dp2 && dp2.DataLength > MaxPartitionDataSize && dp2.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync(dp2, propertyName, strategy, prop, cancellationToken);
        }
    }

    private async Task MergePartitionIfNeededAsync(IPartition partitionToMerge, string propertyName, IPartitionableCrdtStrategy strategy, PropertyInfo prop, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(_metrics.MergePartitionDuration);
        if (partitionToMerge is not DataPartition dataPartitionToMerge) return;

        var logicalKey = dataPartitionToMerge.StartKey.LogicalKey;
        
        DataPartition targetPartition;
        DataPartition sourcePartition;

        if (dataPartitionToMerge.EndKey.HasValue)
        {
            var nextPartitionObj = await _storageService.GetPropertyPartitionAsync(dataPartitionToMerge.EndKey.Value, propertyName, cancellationToken);
            if (nextPartitionObj is not DataPartition nextPartition) return;

            targetPartition = dataPartitionToMerge;
            sourcePartition = nextPartition;
        }
        else
        {
            var partitionCount = await _storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken);
            if (partitionCount < 2) return;

            var previousPartitionObj = await _storageService.GetPropertyPartitionByIndexAsync(logicalKey, partitionCount - 2, propertyName, cancellationToken);
            if (previousPartitionObj is not DataPartition previousPartition) return;

            targetPartition = previousPartition;
            sourcePartition = dataPartitionToMerge;
        }
        
        var targetDocument = await _storageService.LoadPartitionContentAsync<T>(logicalKey, propertyName, targetPartition, cancellationToken);
        var sourceDocument = await _storageService.LoadPartitionContentAsync<T>(logicalKey, propertyName, sourcePartition, cancellationToken);
        
        var mergedContent = strategy.Merge(targetDocument.Data!, targetDocument.Metadata!, sourceDocument.Data!, sourceDocument.Metadata!, prop);
        var mergedEmpty = new DataPartition(targetPartition.StartKey, sourcePartition.EndKey, 0, 0, 0, 0);
        var mergedPartition = await _storageService.SavePartitionContentAsync(logicalKey, propertyName, mergedEmpty, (T)mergedContent.Data, mergedContent.Metadata, cancellationToken);

        await _storageService.DeletePropertyPartitionAsync(propertyName, targetPartition, cancellationToken);
        await _storageService.DeletePropertyPartitionAsync(propertyName, sourcePartition, cancellationToken);
        await _storageService.InsertPropertyPartitionAsync(propertyName, mergedPartition, cancellationToken);
        
        _metrics.PartitionsMerged.Add(1);
    }

    private async Task<IPartition> PersistPartitionChangesAsync(IComparable logicalKey, IPartition partitionToUpdate, T newData, CrdtMetadata newMeta, string? propertyName, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(_metrics.PersistChangesDuration);

        if (partitionToUpdate is HeaderPartition hp)
        {
            var updatedHeader = await _storageService.SaveHeaderPartitionContentAsync(logicalKey, hp, newData, newMeta, cancellationToken);
            await _storageService.UpdateHeaderPartitionAsync(logicalKey, updatedHeader, cancellationToken);
            return updatedHeader;
        }
        else if (partitionToUpdate is DataPartition dp)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            var updatedData = await _storageService.SavePartitionContentAsync(logicalKey, propertyName, dp, newData, newMeta, cancellationToken);
            await _storageService.UpdatePropertyPartitionAsync(propertyName, updatedData, cancellationToken);
            return updatedData;
        }
        else
        {
            throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}");
        }
    }

    private IComparable GetLogicalKey(T obj)
    {
        var logicalKeyObj = PocoPathHelper.GetAccessor(_partitionKeyProperty).Getter(obj) ?? throw new InvalidOperationException($"Partition key property '{_partitionKeyProperty.Name}' cannot be null.");
        if (logicalKeyObj is not IComparable logicalKey)
        {
            throw new InvalidOperationException($"Partition key property '{_partitionKeyProperty.Name}' must implement IComparable.");
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