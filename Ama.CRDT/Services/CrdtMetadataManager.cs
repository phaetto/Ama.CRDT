namespace Ama.CRDT.Services;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
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
    ICrdtStrategyProvider strategyProvider,
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
        PopulateMetadataRecursive(metadata, document, "$", timestamp, document);
        return metadata;
    }

    /// <inheritdoc/>
    public void Initialize<T>(CrdtDocument<T> document) where T : class
    {
        Initialize(document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public void Initialize<T>(CrdtDocument<T> document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(timestamp);

        PopulateMetadataRecursive(document.Metadata, document.Data, "$", timestamp, document.Data);
    }

    /// <inheritdoc/>
    public void Reset<T>(CrdtDocument<T> document) where T : class
    {
        Reset(document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public void Reset<T>(CrdtDocument<T> document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(timestamp);

        document.Metadata.Lww.Clear();
        document.Metadata.PositionalTrackers.Clear();
        document.Metadata.AverageRegisters.Clear();
        document.Metadata.TwoPhaseSets.Clear();
        document.Metadata.LwwSets.Clear();
        document.Metadata.OrSets.Clear();
        document.Metadata.PriorityQueues.Clear();
        document.Metadata.LseqTrackers.Clear();
        document.Metadata.ExclusiveLocks.Clear();
        document.Metadata.LwwMaps.Clear();
        document.Metadata.OrMaps.Clear();
        document.Metadata.CounterMaps.Clear();

        PopulateMetadataRecursive(document.Metadata, document.Data, "$", timestamp, document.Data);
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
        foreach (var kvp in metadata.ExclusiveLocks) { newMetadata.ExclusiveLocks.Add(kvp.Key, kvp.Value); }

        foreach (var kvp in metadata.LwwMaps)
        {
            newMetadata.LwwMaps.Add(kvp.Key, new Dictionary<object, ICrdtTimestamp>(kvp.Value, (kvp.Value as Dictionary<object, ICrdtTimestamp>)?.Comparer));
        }

        foreach (var kvp in metadata.OrMaps)
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

            newMetadata.OrMaps.Add(kvp.Key, (Adds: newAdded, Removes: newRemoved));
        }

        foreach (var kvp in metadata.CounterMaps)
        {
            newMetadata.CounterMaps.Add(kvp.Key, new Dictionary<object, (decimal P, decimal N)>(kvp.Value, (kvp.Value as Dictionary<object, (decimal P, decimal N)>)?.Comparer));
        }

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
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);
        ArgumentNullException.ThrowIfNull(timestamp);

        if (!timestampProvider.IsContinuous)
        {
            return;
        }

        if (!metadata.VersionVector.TryGetValue(replicaId, out var vectorTimestamp))
        {
            metadata.VersionVector[replicaId] = vectorTimestamp = timestampProvider.Init();
        }

        if (vectorTimestamp.CompareTo(timestamp) >= 0)
        {
            return;
        }

        var advancedVector = vectorTimestamp;
        
        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsForReplica = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId && op.Timestamp.CompareTo(vectorTimestamp) > 0)
                .ToLookup(op => op.Timestamp);

            if (exceptionsForReplica.Count > 0)
            {
                foreach (var tsInBetween in timestampProvider.IterateBetween(vectorTimestamp, timestamp))
                {
                    if (!exceptionsForReplica.Contains(tsInBetween))
                    {
                        break;
                    }
                    advancedVector = tsInBetween;
                }
            }
        }
        
        if (!timestampProvider.IterateBetween(advancedVector, timestamp).Any())
        {
            advancedVector = timestamp;
        }

        metadata.VersionVector[replicaId] = advancedVector;

        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsToRemove = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId && op.Timestamp.CompareTo(advancedVector) <= 0)
                .ToList();

            foreach (var exception in exceptionsToRemove)
            {
                metadata.SeenExceptions.Remove(exception);
            }
        }
    }

    /// <inheritdoc/>
    public void ExclusiveLock([DisallowNull] CrdtMetadata metadata, string path, string lockHolderId, [DisallowNull] ICrdtTimestamp timestamp)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockHolderId);
        ArgumentNullException.ThrowIfNull(timestamp);

        if (metadata.ExclusiveLocks.TryGetValue(path, out var currentLock) && currentLock is not null && timestamp.CompareTo(currentLock.Timestamp) <= 0)
        {
            return;
        }

        metadata.ExclusiveLocks[path] = new LockInfo(lockHolderId, timestamp);
    }

    /// <inheritdoc/>
    public void ReleaseLock([DisallowNull] CrdtMetadata metadata, string path, [DisallowNull] ICrdtTimestamp timestamp)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(timestamp);

        if (metadata.ExclusiveLocks.TryGetValue(path, out var currentLock) && currentLock is not null && timestamp.CompareTo(currentLock.Timestamp) <= 0)
        {
            return;
        }

        metadata.ExclusiveLocks[path] = null;
    }

    private void PopulateMetadataRecursive(CrdtMetadata metadata, object obj, string path, ICrdtTimestamp timestamp, object root)
    {
        if (obj is null)
        {
            return;
        }

        if (obj is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value is not null)
                {
                    var valueType = entry.Value.GetType();
                    if (valueType.IsClass && valueType != typeof(string))
                    {
                        var keyString = GetVoterKey(entry.Key);
                        var newPath = $"{path}.['{keyString}']";
                        PopulateMetadataRecursive(metadata, entry.Value, newPath, timestamp, root);
                    }
                }
            }
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
                        PopulateMetadataRecursive(metadata, item, $"{path}[{i}]", timestamp, root);
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

            var strategy = strategyProvider.GetStrategy(propertyInfo);

            InitializeStrategyMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp, root);

            var propertyType = propertyInfo.PropertyType;
            if (propertyValue is IEnumerable and not string || propertyType.IsClass && propertyType != typeof(string))
            {
                PopulateMetadataRecursive(metadata, propertyValue, propertyPath, timestamp, root);
            }
        }
    }

    private void InitializeStrategyMetadata(CrdtMetadata metadata, PropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, ICrdtTimestamp timestamp, object root)
    {
        switch (strategy)
        {
            case LwwStrategy:
                metadata.Lww[propertyPath] = timestamp;
                break;
            case ExclusiveLockStrategy:
                InitializeExclusiveLockMetadata(metadata, propertyInfo, propertyPath, timestamp, root);
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
            case VoteCounterStrategy:
                InitializeVoteCounterMetadata(metadata, propertyPath, propertyValue, timestamp);
                break;
            case TwoPhaseSetStrategy:
            case LwwSetStrategy:
            case OrSetStrategy:
            case PriorityQueueStrategy:
                InitializeSetMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp);
                break;
            case LwwMapStrategy:
            case OrMapStrategy:
            case CounterMapStrategy:
            case MaxWinsMapStrategy:
            case MinWinsMapStrategy:
                InitializeMapMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp);
                break;
        }
    }

    private void InitializeExclusiveLockMetadata(CrdtMetadata metadata, PropertyInfo propertyInfo, string propertyPath, ICrdtTimestamp timestamp, object root)
    {
        if (propertyInfo.GetCustomAttribute<CrdtExclusiveLockStrategyAttribute>() is not { } attr) return;

        var (_, _, value) = PocoPathHelper.ResolvePath(root, attr.LockHolderPropertyPath);
        metadata.ExclusiveLocks[propertyPath] = null;
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

    private void InitializeMapMetadata(CrdtMetadata metadata, PropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, ICrdtTimestamp timestamp)
    {
        if (propertyValue is not IDictionary dictionary) return;

        var keyType = propertyInfo.PropertyType.IsGenericType ? propertyInfo.PropertyType.GetGenericArguments()[0] : typeof(object);
        var comparer = elementComparerProvider.GetComparer(keyType);

        switch (strategy)
        {
            case LwwMapStrategy:
                var lwwMap = new Dictionary<object, ICrdtTimestamp>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key != null)
                    {
                        lwwMap[entry.Key] = timestamp;
                    }
                }
                metadata.LwwMaps[propertyPath] = lwwMap;
                break;

            case OrMapStrategy:
                var orMapAdds = new Dictionary<object, ISet<Guid>>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        orMapAdds[entry.Key] = new HashSet<Guid> { Guid.NewGuid() };
                        var keyString = GetVoterKey(entry.Key);
                        metadata.Lww[$"{propertyPath}.['{keyString}']"] = timestamp;
                    }
                }
                metadata.OrMaps[propertyPath] = (
                    Adds: orMapAdds,
                    Removes: new Dictionary<object, ISet<Guid>>(comparer));
                break;

            case CounterMapStrategy:
                var counterMap = new Dictionary<object, (decimal P, decimal N)>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        var value = Convert.ToDecimal(entry.Value ?? 0);
                        counterMap[entry.Key] = (P: value > 0 ? value : 0, N: value < 0 ? -value : 0);
                    }
                }
                metadata.CounterMaps[propertyPath] = counterMap;
                break;
        }
    }

    private void InitializeVoteCounterMetadata(CrdtMetadata metadata, string propertyPath, object propertyValue, ICrdtTimestamp timestamp)
    {
        if (propertyValue is not IDictionary dictionary) return;

        var voterMap = new Dictionary<object, object>();
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is null || entry.Value is not IEnumerable voters) continue;
            foreach (var voter in voters)
            {
                if (voter != null)
                {
                    voterMap[voter] = entry.Key;
                }
            }
        }

        foreach (var (voter, _) in voterMap)
        {
            var voterKey = GetVoterKey(voter);
            var voterMetaPath = $"{propertyPath}.['{voterKey}']";
            metadata.Lww[voterMetaPath] = timestamp;
        }
    }

    private static string GetVoterKey(object voter)
    {
        return voter switch
        {
            string s => s,
            _ => JsonSerializer.Serialize(voter, DefaultJsonSerializerOptions)
        };
    }
}