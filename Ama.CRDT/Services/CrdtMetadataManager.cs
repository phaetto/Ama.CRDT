namespace Ama.CRDT.Services;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Strategies;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <inheritdoc/>
public sealed class CrdtMetadataManager(
    ICrdtStrategyManager strategyManager, 
    ICrdtTimestampProvider timestampProvider,
    IElementComparerProvider elementComparerProvider) : ICrdtMetadataManager
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    
    /// <inheritdoc/>
    public CrdtMetadata Initialize<T>([DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        return Initialize(document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public CrdtMetadata Initialize<T>([DisallowNull] T document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(timestamp);

        var metadata = new CrdtMetadata();
        PopulateMetadataRecursive(metadata, document, "$", timestamp);
        return metadata;
    }

    /// <inheritdoc/>
    public void InitializeLwwMetadata<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        InitializeLwwMetadata(metadata, document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public void InitializeLwwMetadata<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(timestamp);

        PopulateMetadataRecursive(metadata, document, "$", timestamp);
    }

    /// <inheritdoc/>
    public void Reset<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        Reset(metadata, document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public void Reset<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(timestamp);

        metadata.Lww.Clear();
        metadata.PositionalTrackers.Clear();
        metadata.AverageRegisters.Clear();
        metadata.TwoPhaseSets.Clear();
        metadata.LwwSets.Clear();
        metadata.OrSets.Clear();
        metadata.PriorityQueues.Clear();
        metadata.LseqTrackers.Clear();

        PopulateMetadataRecursive(metadata, document, "$", timestamp);
    }

    /// <inheritdoc/>
    public CrdtMetadata Clone([DisallowNull] CrdtMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var newMetadata = new CrdtMetadata();

        foreach (var kvp in metadata.Lww) { newMetadata.Lww.Add(kvp.Key, kvp.Value); }
        foreach (var kvp in metadata.PositionalTrackers) { newMetadata.PositionalTrackers.Add(kvp.Key, new List<PositionalIdentifier>(kvp.Value)); }
        foreach (var kvp in metadata.AverageRegisters) { newMetadata.AverageRegisters.Add(kvp.Key, new Dictionary<string, AverageRegisterValue>(kvp.Value)); }
        
        foreach (var kvp in metadata.TwoPhaseSets)
        {
            newMetadata.TwoPhaseSets.Add(kvp.Key, (
                Adds: new HashSet<object>(kvp.Value.Adds, (kvp.Value.Adds as HashSet<object>)?.Comparer),
                Tomstones: new HashSet<object>(kvp.Value.Tomstones, (kvp.Value.Tomstones as HashSet<object>)?.Comparer)
            ));
        }

        foreach (var kvp in metadata.LwwSets)
        {
            newMetadata.LwwSets.Add(kvp.Key, (
                Adds: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Adds, (kvp.Value.Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer),
                Removes: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Removes, (kvp.Value.Removes as Dictionary<object, ICrdtTimestamp>)?.Comparer)
            ));
        }

        foreach (var kvp in metadata.OrSets)
        {
            var addedComparer = (kvp.Value.Adds as Dictionary<object, ISet<Guid>>)?.Comparer;
            var newAdded = kvp.Value.Adds.ToDictionary(
                innerKvp => innerKvp.Key,
                innerKvp => (ISet<Guid>)new HashSet<Guid>(innerKvp.Value),
                addedComparer);
            
            var removedComparer = (kvp.Value.Removes as Dictionary<object, ISet<Guid>>)?.Comparer;
            var newRemoved = kvp.Value.Removes.ToDictionary(
                innerKvp => innerKvp.Key,
                innerKvp => (ISet<Guid>)new HashSet<Guid>(innerKvp.Value),
                removedComparer);

            newMetadata.OrSets.Add(kvp.Key, (Adds: newAdded, Removes: newRemoved));
        }
        
        foreach (var kvp in metadata.PriorityQueues)
        {
            newMetadata.PriorityQueues.Add(kvp.Key, (
                Adds: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Adds, (kvp.Value.Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer),
                Removes: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Removes, (kvp.Value.Removes as Dictionary<object, ICrdtTimestamp>)?.Comparer)
            ));
        }

        foreach (var kvp in metadata.LseqTrackers) { newMetadata.LseqTrackers.Add(kvp.Key, new List<LseqItem>(kvp.Value)); }
        foreach (var kvp in metadata.VersionVector) { newMetadata.VersionVector.Add(kvp.Key, kvp.Value); }
        foreach (var op in metadata.SeenExceptions) { newMetadata.SeenExceptions.Add(op); }
        
        return newMetadata;
    }

    /// <inheritdoc/>
    public void PruneLwwTombstones([DisallowNull] CrdtMetadata metadata, [DisallowNull] ICrdtTimestamp threshold)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(threshold);

        var keysToRemove = metadata.Lww
            .Where(kvp => kvp.Value.CompareTo(threshold) < 0)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            metadata.Lww.Remove(key);
        }
    }
    
    /// <inheritdoc/>
    public void AdvanceVersionVector([DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        AdvanceVersionVector(metadata, operation.ReplicaId, operation.Timestamp);
    }

    /// <inheritdoc/>
    public void AdvanceVersionVector([DisallowNull] CrdtMetadata metadata, string replicaId, [DisallowNull] ICrdtTimestamp timestamp)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(timestamp);

        if (string.IsNullOrWhiteSpace(replicaId))
        {
            throw new ArgumentException("Replica ID cannot be null or whitespace.", nameof(replicaId));
        }

        if (metadata.VersionVector.TryGetValue(replicaId, out var currentTimestamp) && timestamp.CompareTo(currentTimestamp) <= 0)
        {
            return;
        }

        metadata.VersionVector[replicaId] = timestamp;

        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsToRemove = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId && op.Timestamp.CompareTo(timestamp) <= 0)
                .ToList();

            foreach (var exception in exceptionsToRemove)
            {
                metadata.SeenExceptions.Remove(exception);
            }
        }
    }

    private void PopulateMetadataRecursive(CrdtMetadata metadata, object obj, string path, ICrdtTimestamp timestamp)
    {
        if (obj is null)
        {
            return;
        }

        if (obj is IEnumerable collection && obj is not string)
        {
            var i = 0;
            foreach (var item in collection)
            {
                if (item is not null)
                {
                    var itemType = item.GetType();
                    if (itemType.IsClass && itemType != typeof(string))
                    {
                        PopulateMetadataRecursive(metadata, item, $"{path}[{i}]", timestamp);
                    }
                }
                i++;
            }
            return;
        }

        var type = obj.GetType();
        foreach (var propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!propertyInfo.CanRead || propertyInfo.GetIndexParameters().Length > 0 || propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            var propertyValue = propertyInfo.GetValue(obj);
            if (propertyValue is null)
            {
                continue;
            }

            var jsonPropertyName = DefaultJsonSerializerOptions.PropertyNamingPolicy?.ConvertName(propertyInfo.Name) ?? propertyInfo.Name;
            var propertyPath = path == "$" ? $"$.{jsonPropertyName}" : $"{path}.{jsonPropertyName}";

            var strategy = strategyManager.GetStrategy(propertyInfo);
            
            InitializeStrategyMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp);
            
            var propertyType = propertyInfo.PropertyType;
            if ((propertyValue is IEnumerable and not string) || (propertyType.IsClass && propertyType != typeof(string)))
            {
                PopulateMetadataRecursive(metadata, propertyValue, propertyPath, timestamp);
            }
        }
    }

    private void InitializeStrategyMetadata(CrdtMetadata metadata, PropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, ICrdtTimestamp timestamp)
    {
        switch (strategy)
        {
            case LwwStrategy:
                metadata.Lww[propertyPath] = timestamp;
                break;
            case ArrayLcsStrategy:
                if (propertyValue is IList lcsList)
                {
                    metadata.PositionalTrackers[propertyPath] = new List<PositionalIdentifier>(
                        Enumerable.Range(0, lcsList.Count).Select(i => new PositionalIdentifier((i + 1).ToString(), Guid.Empty)));
                }
                break;
            case FixedSizeArrayStrategy:
                if (propertyValue is IList fixedList && propertyInfo.GetCustomAttribute<CrdtFixedSizeArrayStrategyAttribute>() is { } fixedSizeAttr)
                {
                    for (var i = 0; i < Math.Min(fixedList.Count, fixedSizeAttr.Size); i++)
                    {
                        metadata.Lww[$"{propertyPath}[{i}]"] = timestamp;
                    }
                }
                break;
            case LseqStrategy:
                if (propertyValue is IList lseqList)
                {
                    var lseqItems = new List<LseqItem>();
                    var baseIdentifier = ImmutableList<(int, string)>.Empty;
                    const int step = 10;
                    for (var i = 0; i < lseqList.Count; i++)
                    {
                        var path = baseIdentifier.Add(((i + 1) * step, "initial"));
                        lseqItems.Add(new LseqItem(new LseqIdentifier(path), lseqList[i]));
                    }
                    metadata.LseqTrackers[propertyPath] = lseqItems;
                }
                break;
            case TwoPhaseSetStrategy:
            case LwwSetStrategy:
            case OrSetStrategy:
            case PriorityQueueStrategy:
                InitializeSetMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp);
                break;
        }
    }

    private void InitializeSetMetadata(CrdtMetadata metadata, PropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, ICrdtTimestamp timestamp)
    {
        if (propertyValue is not IEnumerable collection) return;

        var elementType = propertyInfo.PropertyType.IsGenericType
            ? propertyInfo.PropertyType.GetGenericArguments()[0]
            : propertyInfo.PropertyType.GetElementType() ?? typeof(object);
        var comparer = elementComparerProvider.GetComparer(elementType);
        var collectionAsObjects = collection.Cast<object>().ToList();

        switch (strategy)
        {
            case TwoPhaseSetStrategy:
                metadata.TwoPhaseSets[propertyPath] = (
                    Adds: new HashSet<object>(collectionAsObjects, comparer),
                    Tomstones: new HashSet<object>(comparer));
                break;
            case LwwSetStrategy:
                metadata.LwwSets[propertyPath] = (
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => timestamp, comparer),
                    Removes: new Dictionary<object, ICrdtTimestamp>(comparer));
                break;
            case OrSetStrategy:
                metadata.OrSets[propertyPath] = (
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => (ISet<Guid>)new HashSet<Guid> { Guid.NewGuid() }, comparer),
                    Removes: new Dictionary<object, ISet<Guid>>(comparer));
                break;
            case PriorityQueueStrategy:
                metadata.PriorityQueues[propertyPath] = (
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => timestamp, comparer),
                    Removes: new Dictionary<object, ICrdtTimestamp>(comparer));
                break;
        }
    }
}