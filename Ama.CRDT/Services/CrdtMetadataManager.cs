namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System;
using System.Collections.Generic;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.GarbageCollection;

/// <inheritdoc/>
public sealed class CrdtMetadataManager(
    ICrdtStrategyProvider strategyProvider,
    ICrdtTimestampProvider timestampProvider,
    IElementComparerProvider elementComparerProvider,
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> crdtContexts) : ICrdtMetadataManager
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc/>
    public CrdtMetadata Initialize<T>([DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        // Use the absolute minimum timestamp for baseline initialization so any actual patch operation always wins.
        return Initialize(document, timestampProvider.Create(long.MinValue));
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
        // Use the absolute minimum timestamp for baseline initialization so any actual patch operation always wins.
        Initialize(document, timestampProvider.Create(long.MinValue));
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

        document.Metadata.Epochs.Clear();
        document.Metadata.QuorumApprovals.Clear();
        document.Metadata.Lww.Clear();
        document.Metadata.Fww.Clear();
        document.Metadata.PositionalTrackers.Clear();
        document.Metadata.AverageRegisters.Clear();
        document.Metadata.TwoPhaseSets.Clear();
        document.Metadata.LwwSets.Clear();
        document.Metadata.FwwSets.Clear();
        document.Metadata.OrSets.Clear();
        document.Metadata.PriorityQueues.Clear();
        document.Metadata.SortedSets.Clear();
        document.Metadata.LseqTrackers.Clear();
        document.Metadata.RgaTrackers.Clear();
        document.Metadata.LwwMaps.Clear();
        document.Metadata.FwwMaps.Clear();
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
    public void Compact<T>([DisallowNull] CrdtDocument<T> document, [DisallowNull] ICompactionPolicy policy) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(policy);

        CompactRecursive(document.Metadata, document.Data, "$", policy, document.Data);

        var exceptionsToRemove = document.Metadata.SeenExceptions
            .Where(op => policy.IsSafeToCompact(new CompactionCandidate(
                Timestamp: op.Timestamp,
                ReplicaId: op.ReplicaId,
                Version: op.Clock)))
            .ToList();

        foreach (var ex in exceptionsToRemove)
        {
            document.Metadata.SeenExceptions.Remove(ex);
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

        var typeInfo = PocoPathHelper.GetTypeInfo(obj.GetType(), crdtContexts);
        foreach (var property in typeInfo.Properties.Values)
        {
            if (!property.CanRead)
            {
                continue;
            }

            var propertyValue = property.Getter!(obj);
            if (propertyValue is null)
            {
                continue;
            }

            var propertyPath = path == "$" ? $"$.{property.JsonName}" : $"{path}.{property.JsonName}";

            var strategy = strategyProvider.GetStrategy(typeInfo.Type, property);

            InitializeStrategyMetadata(metadata, property, strategy, propertyPath, propertyValue, timestamp, root);

            var propertyType = property.PropertyType;
            if (propertyValue is IEnumerable and not string || propertyType.IsClass && propertyType != typeof(string))
            {
                PopulateMetadataRecursive(metadata, propertyValue, propertyPath, timestamp, root);
            }
        }
    }
    
    private void CompactRecursive(CrdtMetadata metadata, object obj, string path, ICompactionPolicy policy, object root)
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
                        CompactRecursive(metadata, entry.Value, newPath, policy, root);
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
                        CompactRecursive(metadata, item, $"{path}[{i}]", policy, root);
                    }
                }
                i++;
            }
            return;
        }

        var typeInfo = PocoPathHelper.GetTypeInfo(obj.GetType(), crdtContexts);
        foreach (var property in typeInfo.Properties.Values)
        {
            if (!property.CanRead)
            {
                continue;
            }

            var propertyValue = property.Getter!(obj);
            if (propertyValue is null)
            {
                continue;
            }

            var propertyPath = path == "$" ? $"$.{property.JsonName}" : $"{path}.{property.JsonName}";
            var strategy = strategyProvider.GetStrategy(typeInfo.Type, property);

            strategy.Compact(new CompactionContext(metadata, policy, property.Name, propertyPath, root));

            var propertyType = property.PropertyType;
            if (propertyValue is IEnumerable and not string || propertyType.IsClass && propertyType != typeof(string))
            {
                CompactRecursive(metadata, propertyValue, propertyPath, policy, root);
            }
        }
    }

    private void InitializeStrategyMetadata(CrdtMetadata metadata, CrdtPropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, ICrdtTimestamp timestamp, object root)
    {
        var replicaId = replicaContext.ReplicaId ?? string.Empty;
        var causalTimestamp = new CausalTimestamp(timestamp, replicaId, 0);

        switch (strategy)
        {
            case LwwStrategy:
                metadata.Lww[propertyPath] = causalTimestamp;
                break;
            case FwwStrategy:
                metadata.Fww[propertyPath] = causalTimestamp;
                break;
            case ArrayLcsStrategy:
                if (propertyValue is IList lcsList)
                {
                    metadata.PositionalTrackers[propertyPath] = new List<PositionalIdentifier>(
                        Enumerable.Range(0, lcsList.Count).Select(i => new PositionalIdentifier((i + 1).ToString(), Guid.Empty)));
                }
                break;
            case FixedSizeArrayStrategy:
                if (propertyValue is IList fixedList)
                {
                    for (var i = 0; i < fixedList.Count; i++)
                    {
                        metadata.Lww[$"{propertyPath}[{i}]"] = causalTimestamp;
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
                        var id = new RgaIdentifier(ticksBase + i, replicaContext.ReplicaId!);
                        var item = new RgaItem(id, prevId, rgaList[i], false);
                        rgaItems.Add(item);
                        prevId = id;
                    }
                    metadata.RgaTrackers[propertyPath] = rgaItems;
                }
                break;
            case VoteCounterStrategy:
                InitializeVoteCounterMetadata(metadata, propertyPath, propertyValue, timestamp, replicaId);
                break;
            case TwoPhaseSetStrategy:
            case LwwSetStrategy:
            case FwwSetStrategy:
            case OrSetStrategy:
            case PriorityQueueStrategy:
            case SortedSetStrategy:
                InitializeSetMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp, replicaId);
                break;
            case LwwMapStrategy:
            case FwwMapStrategy:
            case OrMapStrategy:
            case CounterMapStrategy:
            case MaxWinsMapStrategy:
            case MinWinsMapStrategy:
                InitializeMapMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp, replicaId);
                break;
            case TwoPhaseGraphStrategy:
                InitializeTwoPhaseGraphMetadata(metadata, propertyPath, propertyValue);
                break;
            case ReplicatedTreeStrategy:
                InitializeReplicatedTreeMetadata(metadata, propertyPath, propertyValue, timestamp, replicaId);
                break;
        }
    }

    private void InitializeSetMetadata(CrdtMetadata metadata, CrdtPropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, ICrdtTimestamp timestamp, string replicaId)
    {
        if (propertyValue is not IEnumerable collection) return;

        var typeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
        var elementType = typeInfo.CollectionElementType ?? typeof(object);
        var comparer = elementComparerProvider.GetComparer(elementType);
        var collectionAsObjects = collection.Cast<object>().ToList();

        switch (strategy)
        {
            case TwoPhaseSetStrategy:
                metadata.TwoPhaseSets[propertyPath] = new TwoPhaseSetState(
                    Adds: new HashSet<object>(collectionAsObjects, comparer),
                    Tombstones: new Dictionary<object, CausalTimestamp>(comparer));
                break;
            case LwwSetStrategy:
                metadata.LwwSets[propertyPath] = new LwwSetState(
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => timestamp, comparer),
                    Removes: new Dictionary<object, CausalTimestamp>(comparer));
                break;
            case FwwSetStrategy:
                metadata.FwwSets[propertyPath] = new LwwSetState(
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => timestamp, comparer),
                    Removes: new Dictionary<object, CausalTimestamp>(comparer));
                break;
            case OrSetStrategy:
                metadata.OrSets[propertyPath] = new OrSetState(
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => (ISet<Guid>)new HashSet<Guid> { Guid.NewGuid() }, comparer),
                    Removes: new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer));
                break;
            case PriorityQueueStrategy:
                metadata.PriorityQueues[propertyPath] = new LwwSetState(
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => timestamp, comparer),
                    Removes: new Dictionary<object, CausalTimestamp>(comparer));
                break;
            case SortedSetStrategy:
                metadata.SortedSets[propertyPath] = new LwwSetState(
                    Adds: collectionAsObjects.ToDictionary(k => k, _ => timestamp, comparer),
                    Removes: new Dictionary<object, CausalTimestamp>(comparer));
                break;
        }
    }

    private void InitializeMapMetadata(CrdtMetadata metadata, CrdtPropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, ICrdtTimestamp timestamp, string replicaId)
    {
        if (propertyValue is not IDictionary dictionary) return;

        var typeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        var comparer = elementComparerProvider.GetComparer(keyType);

        switch (strategy)
        {
            case LwwMapStrategy:
                var lwwMap = new Dictionary<object, CausalTimestamp>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key != null)
                    {
                        lwwMap[entry.Key] = new CausalTimestamp(timestamp, replicaId, 0);
                    }
                }
                metadata.LwwMaps[propertyPath] = lwwMap;
                break;

            case FwwMapStrategy:
                var fwwMap = new Dictionary<object, CausalTimestamp>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key != null)
                    {
                        fwwMap[entry.Key] = new CausalTimestamp(timestamp, replicaId, 0);
                    }
                }
                metadata.FwwMaps[propertyPath] = fwwMap;
                break;

            case OrMapStrategy:
                var orMapAdds = new Dictionary<object, ISet<Guid>>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        orMapAdds[entry.Key] = new HashSet<Guid> { Guid.NewGuid() };
                        var keyString = GetVoterKey(entry.Key);
                        metadata.Lww[$"{propertyPath}.['{keyString}']"] = new CausalTimestamp(timestamp, replicaId, 0);
                    }
                }
                metadata.OrMaps[propertyPath] = new OrSetState(
                    Adds: orMapAdds,
                    Removes: new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(comparer));
                break;

            case CounterMapStrategy:
                var counterMap = new Dictionary<object, PnCounterState>(comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        var value = PocoPathHelper.ConvertTo<decimal>(entry.Value ?? 0m, crdtContexts);
                        counterMap[entry.Key] = new PnCounterState(P: value > 0 ? value : 0, N: value < 0 ? -value : 0);
                    }
                }
                metadata.CounterMaps[propertyPath] = counterMap;
                break;
        }
    }

    private void InitializeVoteCounterMetadata(CrdtMetadata metadata, string propertyPath, object propertyValue, ICrdtTimestamp timestamp, string replicaId)
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
            metadata.Lww[voterMetaPath] = new CausalTimestamp(timestamp, replicaId, 0);
        }
    }
    
    private void InitializeTwoPhaseGraphMetadata(CrdtMetadata metadata, string propertyPath, object propertyValue)
    {
        var typeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
        if (!typeInfo.Properties.TryGetValue("Vertices", out var verticesProp) || !verticesProp.CanRead) return;
        if (!typeInfo.Properties.TryGetValue("Edges", out var edgesProp) || !edgesProp.CanRead) return;

        var vertices = verticesProp.Getter!(propertyValue) as IEnumerable;
        var edges = edgesProp.Getter!(propertyValue) as IEnumerable;

        if (vertices is null || edges is null) return;
        
        var vertexComparer = elementComparerProvider.GetComparer(typeof(object));
        var edgeComparer = elementComparerProvider.GetComparer(typeof(Edge));
        
        metadata.TwoPhaseGraphs[propertyPath] = new TwoPhaseGraphState(
            VertexAdds: new HashSet<object>(vertices.Cast<object>(), vertexComparer),
            VertexTombstones: new Dictionary<object, CausalTimestamp>(vertexComparer),
            EdgeAdds: new HashSet<object>(edges.Cast<object>(), edgeComparer),
            EdgeTombstones: new Dictionary<object, CausalTimestamp>(edgeComparer)
        );
    }
    
    private void InitializeReplicatedTreeMetadata(CrdtMetadata metadata, string propertyPath, object propertyValue, ICrdtTimestamp timestamp, string replicaId)
    {
        var typeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
        if (!typeInfo.Properties.TryGetValue("Nodes", out var nodesProp) || !nodesProp.CanRead) return;

        if (nodesProp.Getter!(propertyValue) is not IDictionary nodesDictionary) return;

        var idComparer = elementComparerProvider.GetComparer(typeof(object));

        var adds = new Dictionary<object, ISet<Guid>>(idComparer);
        foreach (DictionaryEntry entry in nodesDictionary)
        {
            if (entry.Key is null || entry.Value is null) continue;

            adds[entry.Key] = new HashSet<Guid> { Guid.NewGuid() };
            
            var nodeIdString = GetVoterKey(entry.Key);
            metadata.Lww[$"{propertyPath}.Nodes.['{nodeIdString}'].Value"] = new CausalTimestamp(timestamp, replicaId, 0);
            metadata.Lww[$"{propertyPath}.Nodes.['{nodeIdString}'].ParentId"] = new CausalTimestamp(timestamp, replicaId, 0);
        }

        metadata.ReplicatedTrees[propertyPath] = new OrSetState(
            Adds: adds,
            Removes: new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(idComparer)
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