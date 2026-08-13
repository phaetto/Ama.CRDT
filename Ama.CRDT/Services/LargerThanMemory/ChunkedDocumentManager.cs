namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.LargerThanMemory;
using Ama.CRDT.Services;
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
/// Manages querying and initialization of a CRDT document that is chunked, allowing it to scale beyond available memory by storing data and an index in streams.
/// It uses a user-friendly API with property names and translates them to internal property paths for strategy execution.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class ChunkedDocumentManager<T> : IVirtualDocumentManager<T>, IVirtualDocumentPatchHandler<T> where T : class, new()
{
    public const int MaxChunkItemCount = 100;
    public const int MinChunkItemCount = MaxChunkItemCount / 4;

    private readonly IChunkStorageService storageService;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly LargerThanMemoryManagerCrdtMetrics metrics; 
    private readonly IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories;
    private readonly IEnumerable<CrdtAotContext> aotContexts;

    private readonly CrdtPropertyInfo? partitionKeyProperty;
    private readonly IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IChunkableCollectionStrategy Strategy)> chunkableProperties;
    private readonly IReadOnlyDictionary<string, string> propertyNamePathCache;

    public ChunkedDocumentManager(
        IChunkStorageService storageService,
        ICrdtMetadataManager metadataManager,
        ICrdtStrategyProvider strategyProvider,
        ReplicaContext replicaContext,
        LargerThanMemoryManagerCrdtMetrics metrics,
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
            throw new InvalidOperationException($"The service '{nameof(ChunkedDocumentManager<T>)}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}.");
        }

        this.storageService = storageService;
        this.metadataManager = metadataManager;
        this.metrics = metrics;
        this.compactionPolicyFactories = compactionPolicyFactories;
        this.aotContexts = aotContexts;

        this.partitionKeyProperty = FindPartitionKeyProperty(typeof(T), aotContexts);
        this.chunkableProperties = FindChunkablePropertiesAndStrategies(strategyProvider, aotContexts);
        this.propertyNamePathCache = this.chunkableProperties.ToDictionary(kvp => kvp.Value.Property.Name, kvp => kvp.Key);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(T initialObject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialObject);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.InitializationDuration);

        var logicalKey = GetLogicalKey(initialObject);

        await InitializeHeaderAsync(logicalKey, initialObject, cancellationToken).ConfigureAwait(false);
        await InitializePropertiesAsync(logicalKey, initialObject, cancellationToken).ConfigureAwait(false);
    }
    
    /// <inheritdoc/>
    public async Task<T?> GetFullObjectAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetFullObjectDuration);

        var headerDoc = await GetHeaderChunkContentAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        if (headerDoc is null)
        {
            return null;
        }

        var fullObject = headerDoc.Value.Data!;

        foreach (var (_, (prop, _)) in this.chunkableProperties)
        {
            var collection = prop.Getter!(fullObject);
            if (collection is not null)
            {
                PocoPathHelper.ClearCollection(collection, this.aotContexts);
            }

            await foreach(var chunk in GetAllDataChunksAsync(logicalKey, prop.Name, cancellationToken).WithCancellation(cancellationToken))
            {
                var chunkDoc = await this.storageService.LoadChunkContentAsync<T>(logicalKey, prop.Name, chunk, cancellationToken).ConfigureAwait(false);

                var chunkCollection = prop.Getter!(chunkDoc.Data!);
                
                if (collection is not null && chunkCollection is IEnumerable penum)
                {
                    var typeInfo = PocoPathHelper.GetTypeInfo(collection.GetType(), this.aotContexts);
                    if (typeInfo.IsCollection && typeInfo.CollectionAdd != null)
                    {
                        foreach(var item in penum)
                        {
                            typeInfo.CollectionAdd(collection, item);
                        }
                    }
                    else if (typeInfo.IsDictionary && collection is IDictionary dict && chunkCollection is IDictionary pdict)
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
    public async Task<IChunk?> GetHeaderChunkAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetPartitionDuration);
        return await this.storageService.GetHeaderChunkAsync(logicalKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetHeaderChunkContentAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetPartitionContentDuration);

        var headerChunk = await GetHeaderChunkAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        if (headerChunk is null)
        {
            return null;
        }

        return await this.storageService.LoadHeaderChunkContentAsync<T>(logicalKey, (HeaderChunk)headerChunk, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IChunk?> GetDataChunkAsync(CompositeChunkKey key, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetPartitionDuration);
        
        return await this.storageService.GetPropertyChunkAsync(key, propertyName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetDataChunkContentAsync(CompositeChunkKey key, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetPartitionContentDuration);
        
        var chunk = await this.storageService.GetPropertyChunkAsync(key, propertyName, cancellationToken).ConfigureAwait(false);

        if (chunk is not CollectionChunk)
        {
            return null;
        }

        var dataDoc = await this.storageService.LoadChunkContentAsync<T>(key.LogicalKey, propertyName, chunk, cancellationToken).ConfigureAwait(false);

        var headerDoc = await GetHeaderChunkContentAsync(key.LogicalKey, cancellationToken).ConfigureAwait(false);
        if (headerDoc is null)
        {
            throw new InvalidOperationException($"Could not find header chunk for logical key '{key.LogicalKey}'.");
        }

        var propertyPath = ToPropertyPath(propertyName);
        var (prop, _) = this.chunkableProperties[propertyPath];

        // Attach the chunk's data collection to the header document
        var collection = prop.Getter!(dataDoc.Data!);
        prop.Setter!(headerDoc.Value.Data!, collection);

        var mergedMeta = CrdtMetadata.Merge(headerDoc.Value.Metadata!, dataDoc.Metadata!);
        return new CrdtDocument<T>(headerDoc.Value.Data, mergedMeta);
    }
    
    /// <inheritdoc/>
    public async IAsyncEnumerable<IChunk> GetAllDataChunksAsync(IComparable logicalKey, string propertyName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetAllDataPartitionsDuration);
        
        await foreach (var chunk in this.storageService.GetChunksAsync(logicalKey, propertyName, cancellationToken).WithCancellation(cancellationToken))
        {
            if (chunk is CollectionChunk collectionChunk)
            {
                yield return collectionChunk;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetDataChunkCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetDataPartitionCountDuration);
        
        return await this.storageService.GetPropertyChunkCountAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IChunk?> GetDataChunkByIndexAsync(IComparable logicalKey, long index, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetDataPartitionByIndexDuration);
        
        if (index < 0) return null;
        
        return await this.storageService.GetPropertyChunkByIndexAsync(logicalKey, index, propertyName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetAllLogicalKeysDuration);
        await this.storageService.InitializeHeaderIndexAsync(cancellationToken).ConfigureAwait(false);
        
        var logicalKeys = new HashSet<IComparable>();
        await foreach (var chunk in this.storageService.GetAllHeaderChunksAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            logicalKeys.Add(chunk.GetPartitionKey().LogicalKey);
        }
        return logicalKeys;
    }

    /// <inheritdoc/>
    public async Task CompactAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (!this.compactionPolicyFactories.Any())
        {
            return;
        }

        var logicalKeys = await GetAllLogicalKeysAsync(cancellationToken).ConfigureAwait(false);

        foreach (var logicalKey in logicalKeys)
        {
            // Compact the Header Chunk
            var headerChunk = await GetHeaderChunkAsync(logicalKey, cancellationToken).ConfigureAwait(false);
            if (headerChunk is HeaderChunk hp)
            {
                var headerDoc = await this.storageService.LoadHeaderChunkContentAsync<T>(logicalKey, hp, cancellationToken).ConfigureAwait(false);
                foreach (var factory in this.compactionPolicyFactories)
                {
                    var policy = factory.CreatePolicy();
                    this.metadataManager.Compact(headerDoc, policy);
                }

                var compactedHeader = await this.storageService.SaveHeaderChunkContentAsync(logicalKey, hp, headerDoc.Data!, headerDoc.Metadata!, cancellationToken).ConfigureAwait(false);
                await this.storageService.InsertHeaderChunkAsync(logicalKey, compactedHeader, cancellationToken).ConfigureAwait(false);
            }

            // Compact the Property Data Chunks
            foreach (var (_, (prop, _)) in this.chunkableProperties)
            {
                var chunksToCompact = new List<CollectionChunk>();
                await foreach (var chunk in GetAllDataChunksAsync(logicalKey, prop.Name, cancellationToken).WithCancellation(cancellationToken))
                {
                    if (chunk is CollectionChunk dp)
                    {
                        chunksToCompact.Add(dp);
                    }
                }

                foreach (var dataChunk in chunksToCompact)
                {
                    var crdtDoc = await this.storageService.LoadChunkContentAsync<T>(logicalKey, prop.Name, dataChunk, cancellationToken).ConfigureAwait(false);
                    
                    foreach (var factory in this.compactionPolicyFactories)
                    {
                        var policy = factory.CreatePolicy();
                        this.metadataManager.Compact(crdtDoc, policy);
                    }

                    var compactedChunk = await this.storageService.SaveChunkContentAsync(
                        logicalKey,
                        prop.Name,
                        dataChunk,
                        crdtDoc.Data!,
                        crdtDoc.Metadata!,
                        cancellationToken).ConfigureAwait(false);

                    await this.storageService.DeletePropertyChunkAsync(prop.Name, dataChunk, cancellationToken).ConfigureAwait(false);
                    await this.storageService.InsertPropertyChunkAsync(prop.Name, compactedChunk, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ApplyPatchResult<T>?> TryApplyPatchAsync(IAsyncCrdtApplicator innerApplicator, CrdtDocument<T> document, CrdtPatch patch, CancellationToken cancellationToken = default)
    {
        if (this.partitionKeyProperty is null || this.chunkableProperties.Count == 0)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(document.Data);
        using var _ = new MetricTimer(this.metrics.ApplyPatchDuration);

        var unappliedOperations = new List<UnappliedOperation>();

        if (patch.Operations is null || !patch.Operations.Any())
        {
            return new ApplyPatchResult<T>(document, unappliedOperations);
        }

        var logicalKey = GetLogicalKey(document.Data);
        this.metrics.PatchesApplied.Add(1);

        var headerPartition = await this.storageService.GetHeaderChunkAsync(logicalKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not find header chunk for logical key '{logicalKey}'.");
        var headerDoc = await this.storageService.LoadHeaderChunkContentAsync<T>(logicalKey, (HeaderChunk)headerPartition, cancellationToken).ConfigureAwait(false);

        var groupedOperations = GroupOperationsByProperty(patch.Operations, this.chunkableProperties);
        bool headerModified = groupedOperations.HeaderOps.Count > 0;

        if (groupedOperations.HeaderOps.Count > 0)
        {
            var headerResult = await innerApplicator.ApplyPatchAsync(headerDoc, new CrdtPatch(groupedOperations.HeaderOps.AsReadOnly()), cancellationToken).ConfigureAwait(false);
            unappliedOperations.AddRange(headerResult.UnappliedOperations);
        }

        foreach (var kvp in groupedOperations.PropertyOps)
        {
            var propertyName = kvp.Key;
            var operations = kvp.Value;
            var propertyPath = this.propertyNamePathCache[propertyName];
            var config = this.chunkableProperties[propertyPath];

            var opsByPartition = await GroupOperationsByPartitionAsync(logicalKey, propertyName, config.Strategy, config.Property, propertyPath, operations, cancellationToken).ConfigureAwait(false);
            
            foreach(var partitionOps in opsByPartition)
            {
                var partition = partitionOps.Key;
                var ops = partitionOps.Value;

                var dataDoc = await this.storageService.LoadChunkContentAsync<T>(logicalKey, propertyName, partition, cancellationToken).ConfigureAwait(false);

                // Temporarily inject global synchronization state into the data partition's metadata 
                dataDoc.Metadata!.VersionVector = headerDoc.Metadata!.VersionVector;
                dataDoc.Metadata.SeenExceptions = headerDoc.Metadata.SeenExceptions;

                ApplyPatchResult<T> dataResult;
                using (new MetricTimer(this.metrics.ApplicatorApplyPatchDuration))
                {
                    dataResult = await innerApplicator.ApplyPatchAsync(dataDoc, new CrdtPatch(ops.AsReadOnly()), cancellationToken).ConfigureAwait(false);
                }
                unappliedOperations.AddRange(dataResult.UnappliedOperations);

                // Remove global state from the data partition's metadata so it is not persisted in the data stream.
                dataDoc.Metadata.VersionVector = new Dictionary<string, long>();
                dataDoc.Metadata.SeenExceptions = new HashSet<CrdtOperation>();

                var updatedPartition = await PersistPartitionChangesAsync(logicalKey, partition, dataDoc.Data!, dataDoc.Metadata, propertyName, cancellationToken).ConfigureAwait(false);
                
                if (updatedPartition is CollectionChunk updatedDataPartition)
                {
                    var itemCount = GetItemCount(config.Property, dataDoc.Data!);
                    if (itemCount > MaxChunkItemCount)
                    {
                        await SplitChunkAsync(updatedDataPartition, propertyName, config.Strategy, config.Property, cancellationToken).ConfigureAwait(false);
                    }
                    else if (itemCount < MinChunkItemCount)
                    {
                        var partitionCount = await this.storageService.GetPropertyChunkCountAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
                        if (partitionCount > 1)
                        {
                            await MergePartitionIfNeededAsync(updatedDataPartition, propertyName, config.Strategy, config.Property, cancellationToken).ConfigureAwait(false);
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
        
        return new ApplyPatchResult<T>(headerDoc, unappliedOperations);
    }

    private void EnsureConfigured()
    {
        if (this.partitionKeyProperty is null)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' must be decorated with the [{nameof(PartitionKeyAttribute)}] to be used with virtualized collections.");
        }
        if (this.chunkableProperties.Count == 0)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' does not have any properties with a CRDT strategy that supports chunking (implements {nameof(IChunkableCollectionStrategy)}).");
        }
    }

    private async Task InitializeHeaderAsync(IComparable logicalKey, T initialObject, CancellationToken cancellationToken)
    {
        var headerObject = new T();
        var typeInfo = PocoPathHelper.GetTypeInfo(typeof(T), this.aotContexts);
        var properties = typeInfo.Properties.Values.Where(p => p.CanRead && p.CanWrite);

        // Shallow copy all properties to the header
        foreach (var property in properties)
        {
            property.Setter!(headerObject, property.Getter!(initialObject));
        }

        // Isolate the header by providing empty instances of chunkable collections
        foreach (var kvp in this.chunkableProperties.Values)
        {
            var prop = kvp.Property;
            var originalCollection = prop.Getter!(initialObject);

            if (prop.CanWrite && originalCollection is not null)
            {
                var emptyCollection = PocoPathHelper.InstantiateCollection(prop.PropertyType, this.aotContexts);
                prop.Setter!(headerObject, emptyCollection);
            }
        }

        await this.storageService.ClearHeaderDataAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        await this.storageService.InitializeHeaderIndexAsync(cancellationToken).ConfigureAwait(false);

        var headerMetadata = this.metadataManager.Initialize(headerObject);
        var headerChunkKey = new CompositeChunkKey(logicalKey, null);
        var headerChunk = new HeaderChunk(headerChunkKey, 0, 0, 0, 0);

        headerChunk = await this.storageService.SaveHeaderChunkContentAsync(logicalKey, headerChunk, headerObject, headerMetadata, cancellationToken).ConfigureAwait(false);
        await this.storageService.InsertHeaderChunkAsync(logicalKey, headerChunk, cancellationToken).ConfigureAwait(false);
    }

    private async Task InitializePropertiesAsync(IComparable logicalKey, T initialObject, CancellationToken cancellationToken)
    {
        foreach (var (_, (prop, strategy)) in this.chunkableProperties)
        {
            await this.storageService.InitializePropertyIndexAsync(prop.Name, cancellationToken).ConfigureAwait(false);
            await this.storageService.ClearPropertyDataAsync(logicalKey, prop.Name, cancellationToken).ConfigureAwait(false);

            var initialCollection = prop.Getter!(initialObject);
            var dataObject = new T();
            this.partitionKeyProperty!.Setter!(dataObject, logicalKey);
            prop.Setter!(dataObject, initialCollection);

            var dataMetadata = this.metadataManager.Initialize(dataObject);
            var startRangeKey = strategy.GetStartKey(initialObject, prop) ?? strategy.GetMinimumKey(prop);
            
            var dataChunkKey = new CompositeChunkKey(logicalKey, startRangeKey);
            var dataChunk = new CollectionChunk(dataChunkKey, null, 0, 0, 0, 0);

            dataChunk = (CollectionChunk)await this.storageService.SaveChunkContentAsync(logicalKey, prop.Name, dataChunk, dataObject, dataMetadata, cancellationToken).ConfigureAwait(false);
            await this.storageService.InsertPropertyChunkAsync(prop.Name, dataChunk, cancellationToken).ConfigureAwait(false);

            if (GetItemCount(prop, dataObject) > MaxChunkItemCount)
            {
                await SplitChunkAsync(dataChunk, prop.Name, strategy, prop, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SplitChunkAsync(IChunk chunkToSplit, string propertyName, IChunkableCollectionStrategy strategy, CrdtPropertyInfo prop, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(this.metrics.SplitPartitionDuration);
        if (chunkToSplit is not CollectionChunk dataChunkToSplit) return;
        
        this.metrics.PartitionsSplit.Add(1);

        var crdtDoc = await this.storageService.LoadChunkContentAsync<T>(dataChunkToSplit.StartKey.LogicalKey, propertyName, dataChunkToSplit, cancellationToken).ConfigureAwait(false);
        var itemCount = GetItemCount(prop, crdtDoc.Data!);
        
        // 1. Attempt Piggybacked Compaction to avoid the split entirely
        if (this.compactionPolicyFactories.Any())
        {
            foreach (var factory in this.compactionPolicyFactories)
            {
                var policy = factory.CreatePolicy();
                this.metadataManager.Compact(crdtDoc, policy);
            }

            var compactedChunk = await this.storageService.SaveChunkContentAsync(
                dataChunkToSplit.StartKey.LogicalKey,
                propertyName,
                dataChunkToSplit,
                crdtDoc.Data!,
                crdtDoc.Metadata!,
                cancellationToken).ConfigureAwait(false);

            if (compactedChunk is CollectionChunk dp && GetItemCount(prop, crdtDoc.Data!) <= MaxChunkItemCount)
            {
                await this.storageService.DeletePropertyChunkAsync(propertyName, dataChunkToSplit, cancellationToken).ConfigureAwait(false);
                await this.storageService.InsertPropertyChunkAsync(propertyName, dp, cancellationToken).ConfigureAwait(false);
                return;
            }

            dataChunkToSplit = (CollectionChunk)compactedChunk;
        }

        // 2. Proceed with split
        ChunkSplitResult splitResult;
        using (new MetricTimer(this.metrics.StrategySplitDuration))
        {
            splitResult = strategy.SplitToDisjoint(crdtDoc.Data!, crdtDoc.Metadata!, prop);
        }

        var originalKey = dataChunkToSplit.StartKey;
        var p1Key = originalKey;
        var p2Key = new CompositeChunkKey(originalKey.LogicalKey, splitResult.SplitKey);

        var p1Empty = new CollectionChunk(p1Key, p2Key, 0, 0, 0, 0);
        var p2Empty = new CollectionChunk(p2Key, dataChunkToSplit.EndKey, 0, 0, 0, 0);

        var p1 = await this.storageService.SaveChunkContentAsync(originalKey.LogicalKey, propertyName, p1Empty, (T)splitResult.Partition1.Data, splitResult.Partition1.Metadata, cancellationToken).ConfigureAwait(false);
        var p2 = await this.storageService.SaveChunkContentAsync(originalKey.LogicalKey, propertyName, p2Empty, (T)splitResult.Partition2.Data, splitResult.Partition2.Metadata, cancellationToken).ConfigureAwait(false);

        await this.storageService.DeletePropertyChunkAsync(propertyName, dataChunkToSplit, cancellationToken).ConfigureAwait(false);
        await this.storageService.InsertPropertyChunkAsync(propertyName, p1, cancellationToken).ConfigureAwait(false);
        await this.storageService.InsertPropertyChunkAsync(propertyName, p2, cancellationToken).ConfigureAwait(false);

        var p1Count = GetItemCount(prop, (T)splitResult.Partition1.Data);
        if (p1 is CollectionChunk dp1 && p1Count > MaxChunkItemCount && p1Count < itemCount)
        {
            await SplitChunkAsync(dp1, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
        }

        var p2Count = GetItemCount(prop, (T)splitResult.Partition2.Data);
        if (p2 is CollectionChunk dp2 && p2Count > MaxChunkItemCount && p2Count < itemCount)
        {
            await SplitChunkAsync(dp2, propertyName, strategy, prop, cancellationToken).ConfigureAwait(false);
        }
    }

    private GroupedOperations GroupOperationsByProperty(
        IEnumerable<CrdtOperation> operations, 
        IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IChunkableCollectionStrategy Strategy)> chunkablePropertiesDict)
    {
        var propertyOps = new Dictionary<string, List<CrdtOperation>>();
        var headerOps = new List<CrdtOperation>();

        foreach(var op in operations)
        {
            var propertyName = GetPropertyNameFromOperation(op, chunkablePropertiesDict);
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
        
        return new GroupedOperations(headerOps, propertyOps);
    }
    
    private string? GetPropertyNameFromOperation(
        CrdtOperation op, 
        IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IChunkableCollectionStrategy Strategy)> chunkablePropertiesDict)
    {
        if (string.IsNullOrEmpty(op.JsonPath) || op.JsonPath == "$") return null;

        var segments = PocoPathHelper.ParsePath(op.JsonPath);
        if (segments.Length == 0) return null;

        var propertyName = segments[0];
        var fullPath = $"$.{propertyName}";

        return chunkablePropertiesDict.TryGetValue(fullPath, out var val) ? val.Property.Name : null;
    }

    private async Task<Dictionary<IChunk, List<CrdtOperation>>> GroupOperationsByPartitionAsync(
        IComparable logicalKey, 
        string propertyName, 
        IChunkableCollectionStrategy strategy, 
        CrdtPropertyInfo prop, 
        string propertyPath, 
        IEnumerable<CrdtOperation> operations, 
        CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(this.metrics.GroupOperationsDuration);
        var opsByPartition = new Dictionary<IChunk, List<CrdtOperation>>();

        foreach (var op in operations)
        {
            var rangeKey = strategy.GetKeyFromOperation(op, propertyPath);
            var compositeKey = new CompositeChunkKey(logicalKey, rangeKey);

            var partition = await this.storageService.GetPropertyChunkAsync(compositeKey, propertyName, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Could not find chunk for key '{compositeKey}' in property '{propertyName}'.");

            if (!opsByPartition.TryGetValue(partition, out var opList))
            {
                opList = new List<CrdtOperation>();
                opsByPartition[partition] = opList;
            }
            
            opList.Add(op);
        }
        
        return opsByPartition;
    }

    private async Task MergePartitionIfNeededAsync(CollectionChunk dataPartitionToMerge, string propertyName, IChunkableCollectionStrategy strategy, CrdtPropertyInfo prop, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(this.metrics.MergePartitionDuration);
        var logicalKey = dataPartitionToMerge.StartKey.LogicalKey;
        
        CollectionChunk targetPartition;
        CollectionChunk sourcePartition;

        if (dataPartitionToMerge.EndKey.HasValue)
        {
            var nextPartitionObj = await this.storageService.GetPropertyChunkAsync(dataPartitionToMerge.EndKey.Value, propertyName, cancellationToken).ConfigureAwait(false);
            if (nextPartitionObj is not CollectionChunk nextPartition) return;

            targetPartition = dataPartitionToMerge;
            sourcePartition = nextPartition;
        }
        else
        {
            var partitionCount = await this.storageService.GetPropertyChunkCountAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
            if (partitionCount < 2) return;

            var previousPartitionObj = await this.storageService.GetPropertyChunkByIndexAsync(logicalKey, partitionCount - 2, propertyName, cancellationToken).ConfigureAwait(false);
            if (previousPartitionObj is not CollectionChunk previousPartition) return;

            targetPartition = previousPartition;
            sourcePartition = dataPartitionToMerge;
        }
        
        var targetDocument = await this.storageService.LoadChunkContentAsync<T>(logicalKey, propertyName, targetPartition, cancellationToken).ConfigureAwait(false);
        var sourceDocument = await this.storageService.LoadChunkContentAsync<T>(logicalKey, propertyName, sourcePartition, cancellationToken).ConfigureAwait(false);
        
        var mergedContent = strategy.MergeDisjoint(targetDocument.Data!, targetDocument.Metadata!, sourceDocument.Data!, sourceDocument.Metadata!, prop);
        var mergedEmpty = new CollectionChunk(targetPartition.StartKey, sourcePartition.EndKey, 0, 0, 0, 0);
        var mergedPartition = await this.storageService.SaveChunkContentAsync(logicalKey, propertyName, mergedEmpty, (T)mergedContent.Data, mergedContent.Metadata, cancellationToken).ConfigureAwait(false);

        await this.storageService.DeletePropertyChunkAsync(propertyName, targetPartition, cancellationToken).ConfigureAwait(false);
        await this.storageService.DeletePropertyChunkAsync(propertyName, sourcePartition, cancellationToken).ConfigureAwait(false);
        await this.storageService.InsertPropertyChunkAsync(propertyName, mergedPartition, cancellationToken).ConfigureAwait(false);
        
        this.metrics.PartitionsMerged.Add(1);
    }

    private async Task<IChunk> PersistPartitionChangesAsync(IComparable logicalKey, IChunk partitionToUpdate, T newData, CrdtMetadata newMeta, string? propertyName, CancellationToken cancellationToken)
    {
        using var _ = new MetricTimer(this.metrics.PersistChangesDuration);

        if (partitionToUpdate is HeaderChunk hp)
        {
            var updatedHeader = await this.storageService.SaveHeaderChunkContentAsync(logicalKey, hp, newData, newMeta, cancellationToken).ConfigureAwait(false);
            await this.storageService.UpdateHeaderChunkAsync(logicalKey, updatedHeader, cancellationToken).ConfigureAwait(false);
            return updatedHeader;
        }
        else if (partitionToUpdate is CollectionChunk dp)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            var updatedData = await this.storageService.SaveChunkContentAsync(logicalKey, propertyName, dp, newData, newMeta, cancellationToken).ConfigureAwait(false);
            await this.storageService.UpdatePropertyChunkAsync(propertyName, updatedData, cancellationToken).ConfigureAwait(false);
            return updatedData;
        }
        else
        {
            throw new NotSupportedException($"Unknown chunk type: {partitionToUpdate.GetType().Name}");
        }
    }

    private IComparable GetLogicalKey(T obj)
    {
        var logicalKeyObj = this.partitionKeyProperty!.Getter?.Invoke(obj) ?? throw new InvalidOperationException($"Partition key property '{this.partitionKeyProperty.Name}' cannot be null.");
        if (logicalKeyObj is not IComparable logicalKey)
        {
            throw new InvalidOperationException($"Partition key property '{this.partitionKeyProperty.Name}' must implement IComparable.");
        }
        return logicalKey;
    }
    
    private static IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IChunkableCollectionStrategy Strategy)> FindChunkablePropertiesAndStrategies(ICrdtStrategyProvider strategyProvider, IEnumerable<CrdtAotContext> aotContexts)
    {
        var typeInfo = PocoPathHelper.GetTypeInfo(typeof(T), aotContexts);

        return typeInfo.Properties.Values
            .Select(p => new
            {
                Property = p,
                Strategy = strategyProvider.GetStrategy(typeof(T), p),
                Path = $"$.{char.ToLowerInvariant(p.Name[0])}{p.Name[1..]}"
            })
            .Where(x => x.Strategy is IChunkableCollectionStrategy)
            .ToDictionary(x => x.Path, x => (x.Property, (IChunkableCollectionStrategy)x.Strategy!));
    }
    
    private static CrdtPropertyInfo? FindPartitionKeyProperty(Type type, IEnumerable<CrdtAotContext> aotContexts)
    {
        var attr = type.GetCustomAttribute<PartitionKeyAttribute>();
        if (attr is null) return null;
        
        var typeInfo = PocoPathHelper.GetTypeInfo(type, aotContexts);
        if (!typeInfo.Properties.TryGetValue(attr.PropertyName, out var property)) return null;
        
        return property;
    }

    private string ToPropertyPath(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (!this.propertyNamePathCache.TryGetValue(propertyName, out var propertyPath))
        {
            throw new ArgumentException($"Property '{propertyName}' is not a chunkable property on type '{typeof(T).Name}'.", nameof(propertyName));
        }
        return propertyPath;
    }

    private static int GetItemCount(CrdtPropertyInfo prop, T dataObject)
    {
        var collection = prop.Getter?.Invoke(dataObject);
        if (collection == null) return 0;

        if (collection is ICollection col) return col.Count;

        if (collection is IEnumerable en)
        {
            int count = 0;
            var enumerator = en.GetEnumerator();
            while (enumerator.MoveNext()) count++;
            (enumerator as IDisposable)?.Dispose();
            return count;
        }

        return 0;
    }

    private readonly record struct GroupedOperations(List<CrdtOperation> HeaderOps, Dictionary<string, List<CrdtOperation>> PropertyOps);
}