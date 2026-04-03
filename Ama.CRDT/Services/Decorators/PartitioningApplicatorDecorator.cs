namespace Ama.CRDT.Services.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A global decorator that intercepts patch applications. If the document type is partitionable,
/// it slices the patch, loads necessary partitions via the storage service, delegates CRDT math 
/// to the inner applicator, and saves modifications. Otherwise, it simply passes through.
/// </summary>
public sealed class PartitioningApplicatorDecorator : IAsyncCrdtApplicator
{
    private const int MaxPartitionDataSize = 8192;
    private const int MinPartitionDataSize = MaxPartitionDataSize / 4;

    private readonly IAsyncCrdtApplicator innerApplicator;
    private readonly IPartitionStorageService storageService;
    private readonly ICrdtStrategyProvider strategyProvider;
    private readonly PartitionManagerCrdtMetrics metrics;
    private readonly IEnumerable<CrdtAotContext> aotContexts;

    public PartitioningApplicatorDecorator(
        IAsyncCrdtApplicator innerApplicator,
        IPartitionStorageService storageService,
        ICrdtStrategyProvider strategyProvider,
        PartitionManagerCrdtMetrics metrics,
        IEnumerable<CrdtAotContext> aotContexts)
    {
        ArgumentNullException.ThrowIfNull(innerApplicator);
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(strategyProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(aotContexts);

        this.innerApplicator = innerApplicator;
        this.storageService = storageService;
        this.strategyProvider = strategyProvider;
        this.metrics = metrics;
        this.aotContexts = aotContexts;
    }

    /// <inheritdoc/>
    public async Task<ApplyPatchResult<TDoc>> ApplyPatchAsync<TDoc>([DisallowNull] CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken = default) where TDoc : class
    {
        ArgumentNullException.ThrowIfNull(document.Data);

        var crdtTypeInfo = PocoPathHelper.GetTypeInfo(typeof(TDoc), aotContexts);

        var attr = typeof(TDoc).GetCustomAttribute<PartitionKeyAttribute>();
        CrdtPropertyInfo? partitionKeyProperty = null;
        if (attr != null)
        {
            crdtTypeInfo.Properties.TryGetValue(attr.PropertyName, out partitionKeyProperty);
        }

        var partitionableProperties = new Dictionary<string, (CrdtPropertyInfo Property, IPartitionableCrdtStrategy Strategy)>();
        var nameToPath = new Dictionary<string, string>();

        foreach (var prop in crdtTypeInfo.Properties.Values)
        {
            if (strategyProvider.GetStrategy(typeof(TDoc), prop) is IPartitionableCrdtStrategy strategy)
            {
                var jsonPath = $"$.{prop.JsonName}";
                partitionableProperties[jsonPath] = (prop, strategy);
                nameToPath[prop.Name] = jsonPath;
            }
        }

        // If the document type is not configured for partitioning, simply pass through to the inner applicator.
        if (partitionKeyProperty is null || partitionableProperties.Count == 0)
        {
            return await innerApplicator.ApplyPatchAsync(document, patch, cancellationToken).ConfigureAwait(false);
        }

        using var _ = new MetricTimer(metrics.ApplyPatchDuration);
        
        var unappliedOperations = new List<UnappliedOperation>();

        if (patch.Operations is null || !patch.Operations.Any())
        {
            return new ApplyPatchResult<TDoc>(document, unappliedOperations);
        }

        var logicalKey = GetLogicalKey(document.Data, partitionKeyProperty);
        metrics.PatchesApplied.Add(1);

        var headerPartition = await storageService.GetHeaderPartitionAsync(logicalKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not find header partition for logical key '{logicalKey}'.");
        var headerDoc = await storageService.LoadHeaderPartitionContentAsync<TDoc>(logicalKey, (HeaderPartition)headerPartition, cancellationToken).ConfigureAwait(false);

        var (headerOps, propertyOps) = GroupOperationsByProperty(patch.Operations, partitionableProperties);
        bool headerModified = headerOps.Count > 0;

        if (headerOps.Count > 0)
        {
            var headerResult = await innerApplicator.ApplyPatchAsync(headerDoc, new CrdtPatch(headerOps.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            unappliedOperations.AddRange(headerResult.UnappliedOperations);
        }

        foreach (var (propertyName, operations) in propertyOps)
        {
            var propertyPath = nameToPath[propertyName];
            var (prop, strategy) = partitionableProperties[propertyPath];

            var opsByPartition = await GroupOperationsByPartitionAsync(logicalKey, propertyName, strategy, prop, propertyPath, operations, cancellationToken).ConfigureAwait(false);
            
            foreach(var (partition, ops) in opsByPartition)
            {
                var dataDoc = await storageService.LoadPartitionContentAsync<TDoc>(logicalKey, propertyName, partition, cancellationToken).ConfigureAwait(false);

                // Temporarily inject global synchronization state into the data partition's metadata 
                dataDoc.Metadata!.VersionVector = headerDoc.Metadata!.VersionVector;
                dataDoc.Metadata.SeenExceptions = headerDoc.Metadata.SeenExceptions;

                ApplyPatchResult<TDoc> dataResult;
                using (new MetricTimer(metrics.ApplicatorApplyPatchDuration))
                {
                    dataResult = await innerApplicator.ApplyPatchAsync(dataDoc, new CrdtPatch(ops.AsReadOnly()), cancellationToken).ConfigureAwait(false);
                }
                unappliedOperations.AddRange(dataResult.UnappliedOperations);

                // Remove global state from the data partition's metadata so it is not persisted in the data stream.
                dataDoc.Metadata.VersionVector = new Dictionary<string, long>();
                dataDoc.Metadata.SeenExceptions = new HashSet<CrdtOperation>();

                var updatedPartition = await PersistPartitionChangesAsync(logicalKey, partition, dataDoc.Data!, dataDoc.Metadata, propertyName, cancellationToken).ConfigureAwait(false);
                
                if (updatedPartition is DataPartition updatedDataPartition)
                {
                    if (updatedDataPartition.DataLength > MaxPartitionDataSize)
                    {
                        await SplitPartitionAsync<TDoc>(updatedDataPartition, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
                    }
                    else if (updatedDataPartition.DataLength < MinPartitionDataSize)
                    {
                        var partitionCount = await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
                        if (partitionCount > 1)
                        {
                            await MergePartitionIfNeededAsync<TDoc>(updatedDataPartition, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                
                headerModified = true; // Data partition operations advance the global clock, requiring a header save.
            }
        }

        if (headerModified)
        {
            await PersistPartitionChangesAsync(logicalKey, headerPartition, headerDoc.Data!, headerDoc.Metadata!, null, cancellationToken).ConfigureAwait(false);
        }
        
        return new ApplyPatchResult<TDoc>(headerDoc, unappliedOperations);
    }

    private (List<CrdtOperation> headerOps, Dictionary<string, List<CrdtOperation>> propertyOps) GroupOperationsByProperty(
        IEnumerable<CrdtOperation> operations, 
        IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IPartitionableCrdtStrategy Strategy)> partitionableProperties)
    {
        var propertyOps = new Dictionary<string, List<CrdtOperation>>();
        var headerOps = new List<CrdtOperation>();

        foreach(var op in operations)
        {
            var propertyName = GetPropertyNameFromOperation(op, partitionableProperties);
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
    
    private string? GetPropertyNameFromOperation(
        CrdtOperation op, 
        IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IPartitionableCrdtStrategy Strategy)> partitionableProperties)
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

    private async Task<Dictionary<IPartition, List<CrdtOperation>>> GroupOperationsByPartitionAsync(
        IComparable logicalKey, 
        string propertyName, 
        IPartitionableCrdtStrategy strategy, 
        CrdtPropertyInfo prop, 
        string propertyPath, 
        IEnumerable<CrdtOperation> operations, 
        CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(metrics.GroupOperationsDuration);
        var opsByPartition = new Dictionary<IPartition, List<CrdtOperation>>();

        foreach (var op in operations)
        {
            var rangeKey = strategy.GetKeyFromOperation(op, propertyPath);
            var compositeKey = new CompositePartitionKey(logicalKey, rangeKey);

            var partition = await storageService.GetPropertyPartitionAsync(compositeKey, propertyName, cancellationToken).ConfigureAwait(false)
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

    private async Task SplitPartitionAsync<TDoc>(DataPartition dataPartitionToSplit, string propertyName, IPartitionableCrdtStrategy strategy, CrdtPropertyInfo prop, CancellationToken cancellationToken) where TDoc : class
    {
        using var _ = new MetricTimer(metrics.SplitPartitionDuration);
        metrics.PartitionsSplit.Add(1);

        var crdtDoc = await storageService.LoadPartitionContentAsync<TDoc>(dataPartitionToSplit.StartKey.LogicalKey, propertyName, dataPartitionToSplit, cancellationToken).ConfigureAwait(false);
        
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

        var p1 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p1Empty, (TDoc)splitResult.Partition1.Data, splitResult.Partition1.Metadata, cancellationToken).ConfigureAwait(false);
        var p2 = await storageService.SavePartitionContentAsync(originalKey.LogicalKey, propertyName, p2Empty, (TDoc)splitResult.Partition2.Data, splitResult.Partition2.Metadata, cancellationToken).ConfigureAwait(false);

        await storageService.DeletePropertyPartitionAsync(propertyName, dataPartitionToSplit, cancellationToken).ConfigureAwait(false);
        await storageService.InsertPropertyPartitionAsync(propertyName, p1, cancellationToken).ConfigureAwait(false);
        await storageService.InsertPropertyPartitionAsync(propertyName, p2, cancellationToken).ConfigureAwait(false);

        if (p1 is DataPartition dp1 && dp1.DataLength > MaxPartitionDataSize && dp1.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync<TDoc>(dp1, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
        }

        if (p2 is DataPartition dp2 && dp2.DataLength > MaxPartitionDataSize && dp2.DataLength < dataPartitionToSplit.DataLength)
        {
            await SplitPartitionAsync<TDoc>(dp2, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MergePartitionIfNeededAsync<TDoc>(DataPartition dataPartitionToMerge, string propertyName, IPartitionableCrdtStrategy strategy, CrdtPropertyInfo prop, CancellationToken cancellationToken) where TDoc : class
    {
        using var _ = new MetricTimer(metrics.MergePartitionDuration);
        var logicalKey = dataPartitionToMerge.StartKey.LogicalKey;
        
        DataPartition targetPartition;
        DataPartition sourcePartition;

        if (dataPartitionToMerge.EndKey.HasValue)
        {
            var nextPartitionObj = await storageService.GetPropertyPartitionAsync(dataPartitionToMerge.EndKey.Value, propertyName, cancellationToken).ConfigureAwait(false);
            if (nextPartitionObj is not DataPartition nextPartition) return;

            targetPartition = dataPartitionToMerge;
            sourcePartition = nextPartition;
        }
        else
        {
            var partitionCount = await storageService.GetPropertyPartitionCountAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
            if (partitionCount < 2) return;

            var previousPartitionObj = await storageService.GetPropertyPartitionByIndexAsync(logicalKey, partitionCount - 2, propertyName, cancellationToken).ConfigureAwait(false);
            if (previousPartitionObj is not DataPartition previousPartition) return;

            targetPartition = previousPartition;
            sourcePartition = dataPartitionToMerge;
        }
        
        var targetDocument = await storageService.LoadPartitionContentAsync<TDoc>(logicalKey, propertyName, targetPartition, cancellationToken).ConfigureAwait(false);
        var sourceDocument = await storageService.LoadPartitionContentAsync<TDoc>(logicalKey, propertyName, sourcePartition, cancellationToken).ConfigureAwait(false);
        
        var mergedContent = strategy.Merge(targetDocument.Data!, targetDocument.Metadata!, sourceDocument.Data!, sourceDocument.Metadata!, prop);
        var mergedEmpty = new DataPartition(targetPartition.StartKey, sourcePartition.EndKey, 0, 0, 0, 0);
        var mergedPartition = await storageService.SavePartitionContentAsync(logicalKey, propertyName, mergedEmpty, (TDoc)mergedContent.Data, mergedContent.Metadata, cancellationToken).ConfigureAwait(false);

        await storageService.DeletePropertyPartitionAsync(propertyName, targetPartition, cancellationToken).ConfigureAwait(false);
        await storageService.DeletePropertyPartitionAsync(propertyName, sourcePartition, cancellationToken).ConfigureAwait(false);
        await storageService.InsertPropertyPartitionAsync(propertyName, mergedPartition, cancellationToken).ConfigureAwait(false);
        
        metrics.PartitionsMerged.Add(1);
    }

    private async Task<IPartition> PersistPartitionChangesAsync<TDoc>(IComparable logicalKey, IPartition partitionToUpdate, TDoc newData, CrdtMetadata newMeta, string? propertyName, CancellationToken cancellationToken) where TDoc : class
    {
        using var _ = new MetricTimer(metrics.PersistChangesDuration);

        if (partitionToUpdate is HeaderPartition hp)
        {
            var updatedHeader = await storageService.SaveHeaderPartitionContentAsync(logicalKey, hp, newData, newMeta, cancellationToken).ConfigureAwait(false);
            await storageService.UpdateHeaderPartitionAsync(logicalKey, updatedHeader, cancellationToken).ConfigureAwait(false);
            return updatedHeader;
        }
        else if (partitionToUpdate is DataPartition dp)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            var updatedData = await storageService.SavePartitionContentAsync(logicalKey, propertyName, dp, newData, newMeta, cancellationToken).ConfigureAwait(false);
            await storageService.UpdatePropertyPartitionAsync(propertyName, updatedData, cancellationToken).ConfigureAwait(false);
            return updatedData;
        }
        else
        {
            throw new NotSupportedException($"Unknown partition type: {partitionToUpdate.GetType().Name}");
        }
    }

    private IComparable GetLogicalKey<TDoc>(TDoc obj, CrdtPropertyInfo partitionKeyProperty)
    {
        var logicalKeyObj = partitionKeyProperty.Getter?.Invoke(obj!) 
            ?? throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' cannot be null.");
        if (logicalKeyObj is not IComparable logicalKey)
        {
            throw new InvalidOperationException($"Partition key property '{partitionKeyProperty.Name}' must implement IComparable.");
        }
        return logicalKey;
    }
}