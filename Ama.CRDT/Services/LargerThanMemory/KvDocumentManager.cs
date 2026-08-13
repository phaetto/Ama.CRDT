namespace Ama.CRDT.Services.LargerThanMemory;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
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
/// Manages a True Key-Value virtualized CRDT document, ensuring infinite scaling without chunk bounds.
/// Uses <see cref="IVirtualCollectionStrategy"/> to route individual operations directly to database rows, eliminating write amplification.
/// </summary>
/// <typeparam name="T">The type of the data model managed by the CRDT.</typeparam>
public sealed class KvDocumentManager<T> : IKvDocumentManager<T>, IVirtualDocumentCollection<T>, IVirtualDocumentPatchHandler<T> where T : class, new()
{
    private readonly IKvStorageService storageService;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly LargerThanMemoryManagerCrdtMetrics metrics; 
    private readonly IEnumerable<ICompactionPolicyFactory> compactionPolicyFactories;
    private readonly IEnumerable<CrdtAotContext> aotContexts;

    private readonly CrdtPropertyInfo? partitionKeyProperty;
    private readonly IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IVirtualCollectionStrategy Strategy)> virtualProperties;
    private readonly IReadOnlyDictionary<string, string> propertyNamePathCache;

    public KvDocumentManager(
        IKvStorageService storageService,
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
            throw new InvalidOperationException($"The service '{nameof(KvDocumentManager<T>)}' can only be resolved from a scope created by {nameof(ICrdtScopeFactory)}.");
        }

        this.storageService = storageService;
        this.metadataManager = metadataManager;
        this.metrics = metrics;
        this.compactionPolicyFactories = compactionPolicyFactories;
        this.aotContexts = aotContexts;

        this.partitionKeyProperty = FindPartitionKeyProperty(typeof(T), aotContexts);
        this.virtualProperties = FindVirtualPropertiesAndStrategies(strategyProvider, aotContexts);
        this.propertyNamePathCache = this.virtualProperties.ToDictionary(kvp => kvp.Value.Property.Name, kvp => kvp.Key);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(T initialObject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialObject);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.InitializationDuration);

        var logicalKey = GetLogicalKey(initialObject);

        await InitializeHeaderAsync(logicalKey, initialObject, cancellationToken).ConfigureAwait(false);
        await InitializePropertiesAsync(logicalKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetHeaderAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetPartitionDuration);
        
        return await this.storageService.LoadHeaderAsync<T>(logicalKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<CrdtDocument<T>?> GetDocumentHeaderAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        return GetHeaderAsync(logicalKey, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CrdtDocument<T>?> GetFullDocumentAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        return GetFullDocumentInternalAsync(logicalKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CrdtDocument<T>?> GetItemAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(itemKey);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetPartitionContentDuration);

        var itemDoc = await this.storageService.LoadItemAsync<T>(logicalKey, propertyName, itemKey, cancellationToken).ConfigureAwait(false);
        if (itemDoc is null)
        {
            return null;
        }

        var headerDoc = await GetHeaderAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        if (headerDoc is null)
        {
            throw new InvalidOperationException($"Could not find header document for logical key '{logicalKey}'.");
        }

        var propertyPath = ToPropertyPath(propertyName);
        var (prop, _) = this.virtualProperties[propertyPath];

        // Attach the item's data collection to the header document to emulate the full object structure for the Applicator
        var collection = prop.Getter!(itemDoc.Value.Data!);
        prop.Setter!(headerDoc.Value.Data!, collection);

        var mergedMeta = CrdtMetadata.Merge(headerDoc.Value.Metadata!, itemDoc.Value.Metadata!);
        return new CrdtDocument<T>(headerDoc.Value.Data, mergedMeta);
    }

    /// <inheritdoc/>
    public async Task SaveItemAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CrdtDocument<T> itemDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(itemKey);
        EnsureConfigured();

        await this.storageService.SaveItemAsync(logicalKey, propertyName, itemKey, itemDocument.Data!, itemDocument.Metadata!, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteItemAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(itemKey);
        EnsureConfigured();

        await this.storageService.DeleteItemAsync(logicalKey, propertyName, itemKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<T?> GetFullObjectAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        var doc = await GetFullDocumentInternalAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        return doc?.Data;
    }

    private async Task<CrdtDocument<T>?> GetFullDocumentInternalAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetFullObjectDuration);

        var headerDoc = await GetHeaderAsync(logicalKey, cancellationToken).ConfigureAwait(false);
        if (headerDoc is null)
        {
            return null;
        }

        var fullObject = headerDoc.Value.Data!;
        var mergedMetadata = headerDoc.Value.Metadata!;

        foreach (var (_, (prop, _)) in this.virtualProperties)
        {
            var collection = prop.Getter!(fullObject);
            if (collection is not null)
            {
                PocoPathHelper.ClearCollection(collection, this.aotContexts);
            }

            await foreach (var kvp in this.storageService.GetItemsAsync<T>(logicalKey, prop.Name, cancellationToken).WithCancellation(cancellationToken))
            {
                var itemDoc = kvp.Value;
                
                mergedMetadata = CrdtMetadata.Merge(mergedMetadata, itemDoc.Metadata!);

                var itemCollection = prop.Getter!(itemDoc.Data!);
                
                if (collection is not null && itemCollection is IEnumerable penum)
                {
                    var typeInfo = PocoPathHelper.GetTypeInfo(collection.GetType(), this.aotContexts);
                    if (typeInfo.IsCollection && typeInfo.CollectionAdd != null)
                    {
                        foreach(var item in penum)
                        {
                            typeInfo.CollectionAdd(collection, item);
                        }
                    }
                    else if (typeInfo.IsDictionary && collection is IDictionary dict && itemCollection is IDictionary pdict)
                    {
                        foreach (DictionaryEntry item in pdict)
                        {
                            dict.Add(item.Key, item.Value);
                        }
                    }
                }
            }
        }

        return new CrdtDocument<T>(fullObject, mergedMetadata);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<IComparable, CrdtDocument<T>>> GetAllItemsAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetAllDataPartitionsDuration);
        
        return this.storageService.GetItemsAsync<T>(logicalKey, propertyName, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TElement> GetElementsAsync<TElement>(IComparable logicalKey, string propertyName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        var propertyPath = ToPropertyPath(propertyName);
        var prop = this.virtualProperties[propertyPath].Property;

        await foreach (var kvp in GetAllItemsAsync(logicalKey, propertyName, cancellationToken).WithCancellation(cancellationToken))
        {
            var itemDoc = kvp.Value;
            var collection = prop.Getter!(itemDoc.Data!);
            
            if (collection is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is TElement element)
                    {
                        yield return element;
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public Task<long> GetElementCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        return GetItemCountAsync(logicalKey, propertyName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<long> GetItemCountAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetDataPartitionCountDuration);
        
        return await this.storageService.GetItemCountAsync(logicalKey, propertyName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IComparable>> GetAllLogicalKeysAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var _ = new MetricTimer(this.metrics.GetAllLogicalKeysDuration);
        await this.storageService.InitializeIndexAsync(cancellationToken).ConfigureAwait(false);
        return await this.storageService.GetAllLogicalKeysAsync(cancellationToken).ConfigureAwait(false);
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
            // Compact the Header
            var headerDoc = await GetHeaderAsync(logicalKey, cancellationToken).ConfigureAwait(false);
            if (headerDoc is not null)
            {
                foreach (var factory in this.compactionPolicyFactories)
                {
                    var policy = factory.CreatePolicy();
                    this.metadataManager.Compact(headerDoc.Value, policy);
                }

                await this.storageService.SaveHeaderAsync(logicalKey, headerDoc.Value.Data!, headerDoc.Value.Metadata!, cancellationToken).ConfigureAwait(false);
            }

            // Compact the virtual property items
            foreach (var (_, (prop, _)) in this.virtualProperties)
            {
                var itemsToCompact = new List<KeyValuePair<IComparable, CrdtDocument<T>>>();
                
                await foreach (var kvp in this.storageService.GetItemsAsync<T>(logicalKey, prop.Name, cancellationToken).WithCancellation(cancellationToken))
                {
                    itemsToCompact.Add(kvp);
                }

                foreach (var kvp in itemsToCompact)
                {
                    var itemKey = kvp.Key;
                    var itemDoc = kvp.Value;
                    
                    foreach (var factory in this.compactionPolicyFactories)
                    {
                        var policy = factory.CreatePolicy();
                        this.metadataManager.Compact(itemDoc, policy);
                    }

                    await this.storageService.SaveItemAsync(logicalKey, prop.Name, itemKey, itemDoc.Data!, itemDoc.Metadata!, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ApplyPatchResult<T>?> TryApplyPatchAsync(IAsyncCrdtApplicator innerApplicator, CrdtDocument<T> document, CrdtPatch patch, CancellationToken cancellationToken = default)
    {
        if (this.partitionKeyProperty is null || this.virtualProperties.Count == 0)
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

        var headerDocNullable = await this.storageService.LoadHeaderAsync<T>(logicalKey, cancellationToken).ConfigureAwait(false);
        if (headerDocNullable is null)
        {
            throw new InvalidOperationException($"Could not find header document for logical key '{logicalKey}'.");
        }
        var headerDoc = headerDocNullable.Value;

        var groupedOperations = GroupOperationsByProperty(patch.Operations, this.virtualProperties);
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
            var config = this.virtualProperties[propertyPath];

            var opsByItemKey = GroupOperationsByItemKey(config.Strategy, propertyPath, operations);

            foreach (var itemKvp in opsByItemKey)
            {
                var itemKey = itemKvp.Key;
                var ops = itemKvp.Value;

                var itemDocNullable = await this.storageService.LoadItemAsync<T>(logicalKey, propertyName, itemKey, cancellationToken).ConfigureAwait(false);
                
                CrdtDocument<T> itemDoc;
                if (itemDocNullable is not null)
                {
                    itemDoc = itemDocNullable.Value;
                }
                else
                {
                    var dataObject = new T();
                    this.partitionKeyProperty.Setter!(dataObject, logicalKey);
                    var dataMetadata = this.metadataManager.Initialize(dataObject);
                    itemDoc = new CrdtDocument<T>(dataObject, dataMetadata);
                }

                // Temporarily inject global synchronization state into the item's metadata
                itemDoc.Metadata!.VersionVector = headerDoc.Metadata!.VersionVector;
                itemDoc.Metadata.SeenExceptions = headerDoc.Metadata.SeenExceptions;

                ApplyPatchResult<T> dataResult;
                using (new MetricTimer(this.metrics.ApplicatorApplyPatchDuration))
                {
                    dataResult = await innerApplicator.ApplyPatchAsync(itemDoc, new CrdtPatch(ops.AsReadOnly()), cancellationToken).ConfigureAwait(false);
                }
                unappliedOperations.AddRange(dataResult.UnappliedOperations);

                // Remove global state from the item's metadata so it is not persisted in the item stream.
                itemDoc.Metadata.VersionVector = new Dictionary<string, long>();
                itemDoc.Metadata.SeenExceptions = new HashSet<CrdtOperation>();

                using (new MetricTimer(this.metrics.PersistChangesDuration))
                {
                    await this.storageService.SaveItemAsync(logicalKey, propertyName, itemKey, itemDoc.Data!, itemDoc.Metadata, cancellationToken).ConfigureAwait(false);
                }
                
                headerModified = true;
            }
        }

        if (headerModified)
        {
            using (new MetricTimer(this.metrics.PersistChangesDuration))
            {
                await this.storageService.SaveHeaderAsync(logicalKey, headerDoc.Data!, headerDoc.Metadata!, cancellationToken).ConfigureAwait(false);
            }
        }
        
        return new ApplyPatchResult<T>(headerDoc, unappliedOperations);
    }

    private void EnsureConfigured()
    {
        if (this.partitionKeyProperty is null)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' must be decorated with the [{nameof(PartitionKeyAttribute)}] to be used with virtualized collections.");
        }
        if (this.virtualProperties.Count == 0)
        {
            throw new NotSupportedException($"The type '{typeof(T).Name}' does not have any properties with a CRDT strategy that supports virtual collections (implements {nameof(IVirtualCollectionStrategy)}).");
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

        // Isolate the header by providing empty instances of virtual collections
        foreach (var kvp in this.virtualProperties.Values)
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
        await this.storageService.InitializeIndexAsync(cancellationToken).ConfigureAwait(false);

        var headerMetadata = this.metadataManager.Initialize(headerObject);
        await this.storageService.SaveHeaderAsync(logicalKey, headerObject, headerMetadata, cancellationToken).ConfigureAwait(false);
    }

    private async Task InitializePropertiesAsync(IComparable logicalKey, CancellationToken cancellationToken)
    {
        foreach (var (_, (prop, _)) in this.virtualProperties)
        {
            await this.storageService.ClearPropertyDataAsync(logicalKey, prop.Name, cancellationToken).ConfigureAwait(false);
        }
    }

    private GroupedOperations GroupOperationsByProperty(
        IEnumerable<CrdtOperation> operations, 
        IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IVirtualCollectionStrategy Strategy)> virtualPropertiesDict)
    {
        var propertyOps = new Dictionary<string, List<CrdtOperation>>();
        var headerOps = new List<CrdtOperation>();

        foreach(var op in operations)
        {
            var propertyName = GetPropertyNameFromOperation(op, virtualPropertiesDict);
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
        IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IVirtualCollectionStrategy Strategy)> virtualPropertiesDict)
    {
        if (string.IsNullOrEmpty(op.JsonPath) || op.JsonPath == "$") return null;

        var segments = PocoPathHelper.ParsePath(op.JsonPath);
        if (segments.Length == 0) return null;

        var propertyName = segments[0];
        var fullPath = $"$.{propertyName}";

        return virtualPropertiesDict.TryGetValue(fullPath, out var val) ? val.Property.Name : null;
    }

    private Dictionary<IComparable, List<CrdtOperation>> GroupOperationsByItemKey(IVirtualCollectionStrategy strategy, string propertyPath, IEnumerable<CrdtOperation> ops)
    {
        var dict = new Dictionary<IComparable, List<CrdtOperation>>();
        foreach (var op in ops)
        {
            var key = strategy.GetKeyFromOperation(op, propertyPath);
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<CrdtOperation>();
                dict[key] = list;
            }
            list.Add(op);
        }
        return dict;
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
    
    private static IReadOnlyDictionary<string, (CrdtPropertyInfo Property, IVirtualCollectionStrategy Strategy)> FindVirtualPropertiesAndStrategies(ICrdtStrategyProvider strategyProvider, IEnumerable<CrdtAotContext> aotContexts)
    {
        var typeInfo = PocoPathHelper.GetTypeInfo(typeof(T), aotContexts);

        return typeInfo.Properties.Values
            .Select(p => new
            {
                Property = p,
                Strategy = strategyProvider.GetStrategy(typeof(T), p),
                Path = $"$.{char.ToLowerInvariant(p.Name[0])}{p.Name[1..]}"
            })
            .Where(x => x.Strategy is IVirtualCollectionStrategy)
            .ToDictionary(x => x.Path, x => (x.Property, (IVirtualCollectionStrategy)x.Strategy!));
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
            throw new ArgumentException($"Property '{propertyName}' is not a virtual property on type '{typeof(T).Name}'.", nameof(propertyName));
        }
        return propertyPath;
    }

    private readonly record struct GroupedOperations(List<CrdtOperation> HeaderOps, Dictionary<string, List<CrdtOperation>> PropertyOps);
}