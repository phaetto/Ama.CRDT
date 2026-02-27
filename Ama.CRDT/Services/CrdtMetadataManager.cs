namespace Ama.CRDT.Services;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;

/// <inheritdoc/>
public sealed class CrdtMetadataManager(
    ICrdtStrategyProvider strategyProvider,
    ICrdtTimestampProvider timestampProvider,
    IElementComparerProvider elementComparerProvider,
    ReplicaContext replicaContext) : ICrdtMetadataManager
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

        if (!string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            if (!metadata.VersionVector.ContainsKey(replicaContext.ReplicaId))
            {
                metadata.VersionVector[replicaContext.ReplicaId] = 0;
            }
        }

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

        if (!string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            if (!document.Metadata.VersionVector.ContainsKey(replicaContext.ReplicaId))
            {
                document.Metadata.VersionVector[replicaContext.ReplicaId] = 0;
            }
        }

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
        document.Metadata.RgaTrackers.Clear();
        document.Metadata.LwwMaps.Clear();
        document.Metadata.OrMaps.Clear();
        document.Metadata.CounterMaps.Clear();
        document.Metadata.TwoPhaseGraphs.Clear();
        document.Metadata.ReplicatedTrees.Clear();

        if (!string.IsNullOrWhiteSpace(replicaContext.ReplicaId))
        {
            if (!document.Metadata.VersionVector.ContainsKey(replicaContext.ReplicaId))
            {
                document.Metadata.VersionVector[replicaContext.ReplicaId] = 0;
            }
        }

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
            newMetadata.TwoPhaseSets.Add(kvp.Key, new TwoPhaseSetState(
                Adds: new HashSet<object>(kvp.Value.Adds, (kvp.Value.Adds as HashSet<object>)?.Comparer),
                Tomstones: new HashSet<object>(kvp.Value.Tomstones, (kvp.Value.Tomstones as HashSet<object>)?.Comparer)
            ));
        }

        foreach (var kvp in metadata.LwwSets)
        {
            newMetadata.LwwSets.Add(kvp.Key, new LwwSetState(
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

            newMetadata.OrSets.Add(kvp.Key, new OrSetState(Adds: newAdded, Removes: newRemoved));
        }

        foreach (var kvp in metadata.PriorityQueues)
        {
            newMetadata.PriorityQueues.Add(kvp.Key, new LwwSetState(
                Adds: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Adds, (kvp.Value.Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer),
                Removes: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Removes, (kvp.Value.Removes as Dictionary<object, ICrdtTimestamp>)?.Comparer)
            ));
        }

        foreach (var kvp in metadata.LseqTrackers) { newMetadata.LseqTrackers.Add(kvp.Key, new List<LseqItem>(kvp.Value)); }
        foreach (var kvp in metadata.RgaTrackers) { newMetadata.RgaTrackers.Add(kvp.Key, new List<RgaItem>(kvp.Value)); }
        foreach (var kvp in metadata.VersionVector) { newMetadata.VersionVector.Add(kvp.Key, kvp.Value); }
        foreach (var op in metadata.SeenExceptions) { newMetadata.SeenExceptions.Add(op); }

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

            newMetadata.OrMaps.Add(kvp.Key, new OrSetState(Adds: newAdded, Removes: newRemoved));
        }

        foreach (var kvp in metadata.CounterMaps)
        {
            newMetadata.CounterMaps.Add(kvp.Key, new Dictionary<object, PnCounterState>(kvp.Value, (kvp.Value as Dictionary<object, PnCounterState>)?.Comparer));
        }
        
        foreach (var kvp in metadata.TwoPhaseGraphs)
        {
            newMetadata.TwoPhaseGraphs.Add(kvp.Key, new TwoPhaseGraphState(
                VertexAdds: new HashSet<object>(kvp.Value.VertexAdds, (kvp.Value.VertexAdds as HashSet<object>)?.Comparer),
                VertexTombstones: new HashSet<object>(kvp.Value.VertexTombstones, (kvp.Value.VertexTombstones as HashSet<object>)?.Comparer),
                EdgeAdds: new HashSet<object>(kvp.Value.EdgeAdds, (kvp.Value.EdgeAdds as HashSet<object>)?.Comparer),
                EdgeTombstones: new HashSet<object>(kvp.Value.EdgeTombstones, (kvp.Value.EdgeTombstones as HashSet<object>)?.Comparer)
            ));
        }

        foreach (var kvp in metadata.ReplicatedTrees)
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

            newMetadata.ReplicatedTrees.Add(kvp.Key, new OrSetState(Adds: newAdded, Removes: newRemoved));
        }

        return newMetadata;
    }

    /// <inheritdoc/>
    public CrdtMetadata Merge(params CrdtMetadata[] metadatas)
    {
        ArgumentNullException.ThrowIfNull(metadatas);

        if (metadatas.Length == 0)
        {
            return new CrdtMetadata();
        }

        if (metadatas.Length == 1)
        {
            return Clone(metadatas[0]);
        }

        var merged = new CrdtMetadata();

        foreach (var metadata in metadatas.Where(m => m is not null))
        {
            foreach (var kvp in metadata.Lww) merged.Lww[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.TwoPhaseSets) merged.TwoPhaseSets[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.LwwSets) merged.LwwSets[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.OrSets) merged.OrSets[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.PriorityQueues) merged.PriorityQueues[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.OrMaps) merged.OrMaps[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.TwoPhaseGraphs) merged.TwoPhaseGraphs[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.ReplicatedTrees) merged.ReplicatedTrees[kvp.Key] = kvp.Value;
            
            foreach (var op in metadata.SeenExceptions) merged.SeenExceptions.Add(op);
            
            foreach (var kvp in metadata.PositionalTrackers) merged.PositionalTrackers[kvp.Key] = new List<PositionalIdentifier>(kvp.Value);
            foreach (var kvp in metadata.LseqTrackers) merged.LseqTrackers[kvp.Key] = new List<LseqItem>(kvp.Value);
            
            foreach (var kvp in metadata.AverageRegisters) merged.AverageRegisters[kvp.Key] = new Dictionary<string, AverageRegisterValue>(kvp.Value);
            foreach (var kvp in metadata.LwwMaps) merged.LwwMaps[kvp.Key] = new Dictionary<object, ICrdtTimestamp>(kvp.Value, (kvp.Value as Dictionary<object, ICrdtTimestamp>)?.Comparer);
            foreach (var kvp in metadata.CounterMaps) merged.CounterMaps[kvp.Key] = new Dictionary<object, PnCounterState>(kvp.Value, (kvp.Value as Dictionary<object, PnCounterState>)?.Comparer);
            
            foreach (var kvp in metadata.RgaTrackers)
            {
                if (!merged.RgaTrackers.TryGetValue(kvp.Key, out var existing))
                {
                    merged.RgaTrackers[kvp.Key] = new List<RgaItem>(kvp.Value);
                }
                else
                {
                    var mergedItemsDict = existing.ToDictionary(x => x.Identifier);
                    foreach (var item in kvp.Value)
                    {
                        if (!mergedItemsDict.TryGetValue(item.Identifier, out var eItem) || (!eItem.IsDeleted && item.IsDeleted))
                        {
                            mergedItemsDict[item.Identifier] = item;
                        }
                    }
                    var mergedItems = mergedItemsDict.Values.ToList();
                    mergedItems.Sort((a, b) => a.Identifier.CompareTo(b.Identifier));
                    merged.RgaTrackers[kvp.Key] = mergedItems;
                }
            }
            
            foreach (var (replicaId, clock) in metadata.VersionVector)
            {
                if (!merged.VersionVector.TryGetValue(replicaId, out var existingClock) || clock > existingClock)
                {
                    merged.VersionVector[replicaId] = clock;
                }
            }
        }

        return merged;
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
        AdvanceVersionVector(metadata, operation.ReplicaId, operation.Clock);
    }

    /// <inheritdoc/>
    public void AdvanceVersionVector([DisallowNull] CrdtMetadata metadata, string replicaId, long clock)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        if (!metadata.VersionVector.TryGetValue(replicaId, out var vectorClock))
        {
            metadata.VersionVector[replicaId] = vectorClock = 0;
        }

        if (vectorClock >= clock)
        {
            return;
        }

        var advancedClock = vectorClock;

        if (clock == vectorClock + 1)
        {
            advancedClock = clock;

            if (metadata.SeenExceptions.Count > 0)
            {
                var exceptionsForReplica = metadata.SeenExceptions
                    .Where(op => op.ReplicaId == replicaId && op.Clock > advancedClock)
                    .Select(op => op.Clock)
                    .ToHashSet();

                while (exceptionsForReplica.Contains(advancedClock + 1))
                {
                    advancedClock++;
                }
            }
        }

        metadata.VersionVector[replicaId] = advancedClock;

        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsToRemove = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId && op.Clock <= advancedClock)
                .ToList();

            foreach (var exception in exceptionsToRemove)
            {
                metadata.SeenExceptions.Remove(exception);
            }
        }
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
                    var baseIdentifier = ImmutableList<LseqPathSegment>.Empty;
                    const int step = 10;
                    for (var i = 0; i < lseqList.Count; i++)
                    {
                        var path = baseIdentifier.Add(new LseqPathSegment((i + 1) * step, "initial"));
                        lseqItems.Add(new LseqItem(new LseqIdentifier(path), lseqList[i]));
                    }
                    metadata.LseqTrackers[propertyPath] = lseqItems;
                }
                break;
            case RgaStrategy:
                if (propertyValue is IList rgaList)
                {
                    var rgaItems = new List<RgaItem>();
                    RgaIdentifier? prevId = null;
                    var ticksBase = DateTime.UtcNow.Ticks;
                    for (var i = 0; i < rgaList.Count; i++)
                    {
                        var id = new RgaIdentifier(ticksBase + i, replicaContext.ReplicaId);
                        var item = new RgaItem(id, prevId, rgaList[i], false);
                        rgaItems.Add(item);
                        prevId = id;
                    }
                    metadata.RgaTrackers[propertyPath] = rgaItems;
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
            case TwoPhaseGraphStrategy:
                InitializeTwoPhaseGraphMetadata(metadata, propertyInfo, propertyPath, propertyValue);
                break;
            case ReplicatedTreeStrategy:
                InitializeReplicatedTreeMetadata(metadata, propertyInfo, propertyPath, propertyValue, timestamp);
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
                metadata.TwoPhaseSets[propertyPath] = new TwoPhaseSetState(
                    Adds: new HashSet<object>(collectionAsObjects, comparer),
                    Tomstones: new HashSet<object>(comparer));
                break;
            case LwwSetStrategy:
                metadata.LwwSets[propertyPath] = new LwwSetState(
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => timestamp, comparer),
                    Removes: new Dictionary<object, ICrdtTimestamp>(comparer));
                break;
            case OrSetStrategy:
                metadata.OrSets[propertyPath] = new OrSetState(
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => (ISet<Guid>)new HashSet<Guid> { Guid.NewGuid() }, comparer),
                    Removes: new Dictionary<object, ISet<Guid>>(comparer));
                break;
            case PriorityQueueStrategy:
                metadata.PriorityQueues[propertyPath] = new LwwSetState(
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
                metadata.OrMaps[propertyPath] = new OrSetState(
                    Adds: orMapAdds,
                    Removes: new Dictionary<object, ISet<Guid>>(comparer));
                break;

            case CounterMapStrategy:
                var counterMap = new Dictionary<object, PnCounterState>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        var value = Convert.ToDecimal(entry.Value ?? 0);
                        counterMap[entry.Key] = new PnCounterState(P: value > 0 ? value : 0, N: value < 0 ? -value : 0);
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
    
    private void InitializeTwoPhaseGraphMetadata(CrdtMetadata metadata, PropertyInfo propertyInfo, string propertyPath, object propertyValue)
    {
        var graphType = propertyValue.GetType();
        var verticesProperty = graphType.GetProperty("Vertices");
        var edgesProperty = graphType.GetProperty("Edges");

        if (verticesProperty is null || edgesProperty is null) return;

        var vertices = verticesProperty.GetValue(propertyValue) as IEnumerable;
        var edges = edgesProperty.GetValue(propertyValue) as IEnumerable;

        if (vertices is null || edges is null) return;
        
        var vertexComparer = elementComparerProvider.GetComparer(typeof(object));
        var edgeComparer = elementComparerProvider.GetComparer(typeof(Edge));
        
        metadata.TwoPhaseGraphs[propertyPath] = new TwoPhaseGraphState(
            VertexAdds: new HashSet<object>(vertices.Cast<object>(), vertexComparer),
            VertexTombstones: new HashSet<object>(vertexComparer),
            EdgeAdds: new HashSet<object>(edges.Cast<object>(), edgeComparer),
            EdgeTombstones: new HashSet<object>(edgeComparer)
        );
    }
    
    private void InitializeReplicatedTreeMetadata(CrdtMetadata metadata, PropertyInfo propertyInfo, string propertyPath, object propertyValue, ICrdtTimestamp timestamp)
    {
        var treeType = propertyValue.GetType();
        var nodesProperty = treeType.GetProperty("Nodes");
        if (nodesProperty is null) return;

        if (nodesProperty.GetValue(propertyValue) is not IDictionary nodesDictionary) return;

        var idComparer = elementComparerProvider.GetComparer(typeof(object));

        var adds = new Dictionary<object, ISet<Guid>>(idComparer);
        foreach (DictionaryEntry entry in nodesDictionary)
        {
            if (entry.Key is null || entry.Value is null) continue;

            adds[entry.Key] = new HashSet<Guid> { Guid.NewGuid() };
            
            var nodeIdString = GetVoterKey(entry.Key);
            metadata.Lww[$"{propertyPath}.Nodes.['{nodeIdString}'].Value"] = timestamp;
            metadata.Lww[$"{propertyPath}.Nodes.['{nodeIdString}'].ParentId"] = timestamp;
        }

        metadata.ReplicatedTrees[propertyPath] = new OrSetState(
            Adds: adds,
            Removes: new Dictionary<object, ISet<Guid>>(idComparer)
        );
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