namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System;
using System.Collections.Generic;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Pooling;
using Ama.CRDT.Services.Serialization;
using Ama.CRDT.Models.Collections;

/// <inheritdoc/>
public sealed class CrdtMetadataManager(
    ICrdtStrategyProvider strategyProvider,
    ICrdtTimestampProvider timestampProvider,
    IElementComparerProvider elementComparerProvider,
    IEnumerable<CrdtAotContext> crdtContexts,
    ICrdtSerializer crdtSerializer,
    ICrdtStringPool? stringPool = null) : ICrdtMetadataManager
{
    private const string InitialReplicaId = "initial";
    private readonly ICrdtStringPool pool = stringPool ?? CrdtStringPool.Shared;

    /// <inheritdoc/>
    public CrdtMetadata Initialize<T>([DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        // Use the absolute minimum timestamp for baseline initialization so any actual patch operation always wins.
        return Initialize(document, timestampProvider.Create(long.MinValue, InitialReplicaId));
    }

    /// <inheritdoc/>
    public CrdtMetadata Initialize<T>([DisallowNull] T document, CrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = new CrdtMetadata();
        ICrdtMetadataState boxedCausalTs = new CausalTimestamp(timestamp, timestamp.ReplicaId ?? InitialReplicaId, 0);
        PopulateMetadataRecursive(metadata, document, "$", timestamp, boxedCausalTs, document);
        return metadata;
    }

    /// <inheritdoc/>
    public void Initialize<T>(CrdtDocument<T> document) where T : class
    {
        // Use the absolute minimum timestamp for baseline initialization so any actual patch operation always wins.
        Initialize(document, timestampProvider.Create(long.MinValue, InitialReplicaId));
    }

    /// <inheritdoc/>
    public void Initialize<T>(CrdtDocument<T> document, CrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);

        ICrdtMetadataState boxedCausalTs = new CausalTimestamp(timestamp, timestamp.ReplicaId ?? InitialReplicaId, 0);
        PopulateMetadataRecursive(document.Metadata, document.Data, "$", timestamp, boxedCausalTs, document.Data);
    }

    /// <inheritdoc/>
    public void Reset<T>(CrdtDocument<T> document) where T : class
    {
        Reset(document, timestampProvider.Now(InitialReplicaId));
    }

    /// <inheritdoc/>
    public void Reset<T>(CrdtDocument<T> document, CrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);

        document.Metadata.States.Clear();

        ICrdtMetadataState boxedCausalTs = new CausalTimestamp(timestamp, timestamp.ReplicaId ?? InitialReplicaId, 0);
        PopulateMetadataRecursive(document.Metadata, document.Data, "$", timestamp, boxedCausalTs, document.Data);
    }

    /// <inheritdoc/>
    public void Compact<T>([DisallowNull] CrdtDocument<T> document, [DisallowNull] ICompactionPolicy policy) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(policy);

        CompactRecursive(document.Metadata, document.Data, "$", policy, document.Data);

        if (document.Metadata.SeenExceptions.Count > 0)
        {
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

        if (CrdtCollectionMemoryOptions.TrimExcessOnMetadataCompaction)
        {
            TrimMetadataCollections(document.Metadata);
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

    /// <inheritdoc/>
    public void EvictReplica([DisallowNull] CrdtMetadata metadata, [DisallowNull] string replicaId)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        metadata.VersionVector.Remove(replicaId);

        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsToRemove = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId)
                .ToList();

            foreach (var exception in exceptionsToRemove)
            {
                metadata.SeenExceptions.Remove(exception);
            }
        }
    }

    /// <inheritdoc/>
    public void EvictReplica<T>([DisallowNull] CrdtDocument<T> document, [DisallowNull] string replicaId) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        EvictReplica(document.Metadata, replicaId);
    }

    /// <inheritdoc/>
    public PackedCrdtDocument<T> Pack<T>(CrdtDocument<T> document) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);

        var bytes = crdtSerializer.SerializeToBytes(document.Metadata);
        return new PackedCrdtDocument<T>(document.Data, bytes);
    }

    /// <inheritdoc/>
    public CrdtDocument<T> Unpack<T>(PackedCrdtDocument<T> packedDocument) where T : class
    {
        if (packedDocument.MetadataBytes.IsEmpty)
        {
            return new CrdtDocument<T>(packedDocument.Data, new CrdtMetadata());
        }

        var metadata = crdtSerializer.DeserializeFromBytes<CrdtMetadata>(packedDocument.MetadataBytes.Span) ?? new CrdtMetadata();
        return new CrdtDocument<T>(packedDocument.Data, metadata);
    }

    /// <inheritdoc/>
    public PackedCrdtDocument<T> InitializePacked<T>([DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = Initialize(document);
        return Pack(new CrdtDocument<T>(document, metadata));
    }

    /// <inheritdoc/>
    public PackedCrdtDocument<T> InitializePacked<T>([DisallowNull] T document, CrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = Initialize(document, timestamp);
        return Pack(new CrdtDocument<T>(document, metadata));
    }

    private void PopulateMetadataRecursive(CrdtMetadata metadata, object obj, string path, CrdtTimestamp timestamp, ICrdtMetadataState boxedCausalTimestamp, object root)
    {
        if (obj is null)
        {
            return;
        }

        if (obj is IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
            {
                if (key is null)
                {
                    continue;
                }

                var val = dictionary[key];
                if (val is not null)
                {
                    var valueType = val.GetType();
                    if (valueType.IsClass && valueType != typeof(string))
                    {
                        var keyString = GetVoterKey(key);
                        var newPath = pool.GetOrAdd($"{path}.['{keyString}']");
                        PopulateMetadataRecursive(metadata, val, newPath, timestamp, boxedCausalTimestamp, root);
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
                        var indexedPath = pool.GetOrAdd($"{path}[{i}]");
                        PopulateMetadataRecursive(metadata, item, indexedPath, timestamp, boxedCausalTimestamp, root);
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

            var propertyPath = path == "$" ? property.RootJsonPath : pool.GetOrAdd($"{path}.{property.JsonName}");

            var strategy = strategyProvider.GetStrategy(typeInfo.Type, property);

            InitializeStrategyMetadata(metadata, property, strategy, propertyPath, propertyValue, timestamp, boxedCausalTimestamp, root);

            var propertyType = property.PropertyType;
            if (ShouldTraversePropertyValue(propertyValue, propertyType))
            {
                PopulateMetadataRecursive(metadata, propertyValue, propertyPath, timestamp, boxedCausalTimestamp, root);
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
            foreach (var key in dictionary.Keys)
            {
                if (key is null)
                {
                    continue;
                }

                var val = dictionary[key];
                if (val is not null)
                {
                    var valueType = val.GetType();
                    if (valueType.IsClass && valueType != typeof(string))
                    {
                        var keyString = GetVoterKey(key);
                        var newPath = pool.GetOrAdd($"{path}.['{keyString}']");
                        CompactRecursive(metadata, val, newPath, policy, root);
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
                        var indexedPath = pool.GetOrAdd($"{path}[{i}]");
                        CompactRecursive(metadata, item, indexedPath, policy, root);
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

            var propertyPath = path == "$" ? property.RootJsonPath : pool.GetOrAdd($"{path}.{property.JsonName}");
            var strategy = strategyProvider.GetStrategy(typeInfo.Type, property);

            strategy.Compact(new CompactionContext(metadata, policy, property.Name, propertyPath, root));

            var propertyType = property.PropertyType;
            if (ShouldTraversePropertyValue(propertyValue, propertyType))
            {
                CompactRecursive(metadata, propertyValue, propertyPath, policy, root);
            }
        }
    }

    private bool ShouldTraversePropertyValue(object propertyValue, Type propertyType)
    {
        if (propertyValue is string)
        {
            return false;
        }

        if (propertyValue is IDictionary)
        {
            var dictTypeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
            var valType = dictTypeInfo.DictionaryValueType;
            return valType is null || (valType.IsClass && valType != typeof(string));
        }

        if (propertyValue is IEnumerable)
        {
            var collTypeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
            var elemType = collTypeInfo.CollectionElementType;
            return elemType is null || (elemType.IsClass && elemType != typeof(string));
        }

        return propertyType.IsClass && propertyType != typeof(string);
    }

    private void InitializeStrategyMetadata(CrdtMetadata metadata, CrdtPropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, CrdtTimestamp timestamp, ICrdtMetadataState boxedCausalTimestamp, object root)
    {
        var replicaId = timestamp.ReplicaId ?? InitialReplicaId;

        switch (strategy)
        {
            case LwwStrategy:
                metadata.States[propertyPath] = boxedCausalTimestamp;
                break;
            case FwwStrategy:
                metadata.States[propertyPath] = new FwwTimestamp(timestamp, replicaId, 0);
                break;
            case BoundedCounterStrategy:
                var initialBoundedVal = PocoPathHelper.ConvertTo<decimal>(propertyValue, crdtContexts);
                var boundedContributions = new CompactDictionary<string, PnCounterState>(initialBoundedVal != 0m ? 1 : 0);
                if (initialBoundedVal != 0m)
                {
                    boundedContributions[string.Empty] = new PnCounterState(
                        initialBoundedVal > 0m ? initialBoundedVal : 0m,
                        initialBoundedVal < 0m ? -initialBoundedVal : 0m);
                }
                metadata.States[propertyPath] = new CounterState(boundedContributions);
                break;
            case ArrayLcsStrategy:
                if (propertyValue is IList lcsList)
                {
                    var positions = new List<PositionalIdentifier>(lcsList.Count);
                    for (var i = 0; i < lcsList.Count; i++)
                    {
                        positions.Add(new PositionalIdentifier((i + 1).ToString(), Guid.Empty));
                    }
                    metadata.States[propertyPath] = new PositionalState(positions);
                }
                break;
            case FixedSizeArrayStrategy:
                if (propertyValue is IList fixedList)
                {
                    for (var i = 0; i < fixedList.Count; i++)
                    {
                        metadata.States[pool.GetOrAdd($"{propertyPath}[{i}]")] = boxedCausalTimestamp;
                    }
                }
                break;
            case LseqStrategy:
                if (propertyValue is IList lseqList)
                {
                    var lseqItems = new List<LseqItem>(lseqList.Count);
                    var baseIdentifier = ImmutableList<LseqPathSegment>.Empty;
                    const int step = 10;
                    for (var i = 0; i < lseqList.Count; i++)
                    {
                        var path = baseIdentifier.Add(new LseqPathSegment((i + 1) * step, InitialReplicaId));
                        lseqItems.Add(new LseqItem(new LseqIdentifier(path), lseqList[i]));
                    }
                    metadata.States[propertyPath] = new LseqState(lseqItems);
                }
                break;
            case RgaStrategy:
                if (propertyValue is IList rgaList)
                {
                    var rgaItems = new List<RgaItem>(rgaList.Count);
                    RgaIdentifier? prevId = null;
                    var ticksBase = DateTime.UtcNow.Ticks;
                    for (var i = 0; i < rgaList.Count; i++)
                    {
                        var id = new RgaIdentifier(ticksBase + i, replicaId);
                        var item = new RgaItem(id, prevId, rgaList[i], false);
                        rgaItems.Add(item);
                        prevId = id;
                    }
                    metadata.States[propertyPath] = new RgaState(rgaItems);
                }
                break;
            case VoteCounterStrategy:
                InitializeVoteCounterMetadata(metadata, propertyPath, propertyValue, boxedCausalTimestamp);
                break;
            case TwoPhaseSetStrategy:
            case LwwSetStrategy:
            case FwwSetStrategy:
            case OrSetStrategy:
            case PriorityQueueStrategy:
            case SortedSetStrategy:
                InitializeSetMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp);
                break;
            case LwwMapStrategy:
            case FwwMapStrategy:
            case OrMapStrategy:
            case CounterMapStrategy:
            case MaxWinsMapStrategy:
            case MinWinsMapStrategy:
                InitializeMapMetadata(metadata, propertyInfo, strategy, propertyPath, propertyValue, timestamp, replicaId, boxedCausalTimestamp);
                break;
            case TwoPhaseGraphStrategy:
                InitializeTwoPhaseGraphMetadata(metadata, propertyPath, propertyValue);
                break;
            case ReplicatedTreeStrategy:
                InitializeReplicatedTreeMetadata(metadata, propertyPath, propertyValue, boxedCausalTimestamp);
                break;
        }
    }

    private void InitializeSetMetadata(CrdtMetadata metadata, CrdtPropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, CrdtTimestamp timestamp)
    {
        if (propertyValue is not IEnumerable collection) return;

        var typeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
        var elementType = typeInfo.CollectionElementType ?? typeof(object);
        var comparer = elementComparerProvider.GetComparer(elementType);
        var initialCount = collection is ICollection coll ? coll.Count : 0;

        switch (strategy)
        {
            case TwoPhaseSetStrategy:
                var twoPhaseAdds = new CompactSet<object>(initialCount, comparer);
                foreach (var item in collection)
                {
                    if (item is not null)
                    {
                        twoPhaseAdds.Add(item);
                    }
                }
                metadata.States[propertyPath] = new TwoPhaseSetState(
                    Adds: twoPhaseAdds,
                    Tombstones: new CompactDictionary<object, CausalTimestamp>(0, comparer));
                break;
            case LwwSetStrategy:
            case PriorityQueueStrategy:
            case SortedSetStrategy:
                var lwwSetAdds = new CompactDictionary<object, CrdtTimestamp>(initialCount, comparer);
                foreach (var item in collection)
                {
                    if (item is not null)
                    {
                        lwwSetAdds[item] = timestamp;
                    }
                }
                metadata.States[propertyPath] = new LwwSetState(
                    Adds: lwwSetAdds,
                    Removes: new CompactDictionary<object, CausalTimestamp>(0, comparer));
                break;
            case FwwSetStrategy:
                var fwwSetAdds = new CompactDictionary<object, CrdtTimestamp>(initialCount, comparer);
                foreach (var item in collection)
                {
                    if (item is not null)
                    {
                        fwwSetAdds[item] = timestamp;
                    }
                }
                metadata.States[propertyPath] = new FwwSetState(
                    Adds: fwwSetAdds,
                    Removes: new CompactDictionary<object, CausalTimestamp>(0, comparer));
                break;
            case OrSetStrategy:
                var orSetAdds = new CompactDictionary<object, ISet<Guid>>(initialCount, comparer);
                foreach (var item in collection)
                {
                    if (item is not null)
                    {
                        var tags = new CompactSet<Guid>(1) { Guid.NewGuid() };
                        orSetAdds[item] = tags;
                    }
                }
                metadata.States[propertyPath] = new OrSetState(
                    Adds: orSetAdds,
                    Removes: new CompactDictionary<object, IDictionary<Guid, CausalTimestamp>>(0, comparer));
                break;
        }
    }

    private void InitializeMapMetadata(CrdtMetadata metadata, CrdtPropertyInfo propertyInfo, ICrdtStrategy strategy, string propertyPath, object propertyValue, CrdtTimestamp timestamp, string replicaId, ICrdtMetadataState boxedCausalTimestamp)
    {
        if (propertyValue is not IDictionary dictionary) return;

        var typeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
        var keyType = typeInfo.DictionaryKeyType ?? typeof(object);
        var comparer = elementComparerProvider.GetComparer(keyType);
        var causalTs = new CausalTimestamp(timestamp, replicaId, 0);

        switch (strategy)
        {
            case LwwMapStrategy:
                var lwwMap = new CompactDictionary<object, CausalTimestamp>(dictionary.Count, comparer);
                foreach (var key in dictionary.Keys)
                {
                    if (key is not null)
                    {
                        lwwMap[key] = causalTs;
                    }
                }
                metadata.States[propertyPath] = new LwwMapState(lwwMap);
                break;

            case FwwMapStrategy:
                var fwwMap = new CompactDictionary<object, CausalTimestamp>(dictionary.Count, comparer);
                foreach (var key in dictionary.Keys)
                {
                    if (key is not null)
                    {
                        fwwMap[key] = causalTs;
                    }
                }
                metadata.States[propertyPath] = new FwwMapState(fwwMap);
                break;

            case OrMapStrategy:
                var orMapAdds = new CompactDictionary<object, ISet<Guid>>(dictionary.Count, comparer);
                foreach (var key in dictionary.Keys)
                {
                    if (key is not null)
                    {
                        orMapAdds[key] = new CompactSet<Guid>(1) { Guid.NewGuid() };
                        var keyString = GetVoterKey(key);
                        metadata.States[pool.GetOrAdd($"{propertyPath}.['{keyString}']")] = boxedCausalTimestamp;
                    }
                }
                metadata.States[propertyPath] = new OrSetState(
                    Adds: orMapAdds,
                    Removes: new CompactDictionary<object, IDictionary<Guid, CausalTimestamp>>(0, comparer));
                break;

            case CounterMapStrategy:
                var counterMap = new CompactDictionary<object, PnCounterState>(dictionary.Count, comparer);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        var value = PocoPathHelper.ConvertTo<decimal>(entry.Value ?? 0m, crdtContexts);
                        counterMap[entry.Key] = new PnCounterState(P: value > 0 ? value : 0, N: value < 0 ? -value : 0);
                    }
                }
                metadata.States[propertyPath] = new CounterMapState(counterMap);
                break;
        }
    }

    private void InitializeVoteCounterMetadata(CrdtMetadata metadata, string propertyPath, object propertyValue, ICrdtMetadataState boxedCausalTimestamp)
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
            var voterMetaPath = pool.GetOrAdd($"{propertyPath}.['{voterKey}']");
            metadata.States[voterMetaPath] = boxedCausalTimestamp;
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
        
        var vertexAdds = new CompactSet<object>(vertices is ICollection vc ? vc.Count : 0, vertexComparer);
        foreach (var v in vertices)
        {
            if (v is not null) vertexAdds.Add(v);
        }

        var edgeAdds = new CompactSet<object>(edges is ICollection ec ? ec.Count : 0, edgeComparer);
        foreach (var e in edges)
        {
            if (e is not null) edgeAdds.Add(e);
        }

        metadata.States[propertyPath] = new TwoPhaseGraphState(
            VertexAdds: vertexAdds,
            VertexTombstones: new CompactDictionary<object, CausalTimestamp>(0, vertexComparer),
            EdgeAdds: edgeAdds,
            EdgeTombstones: new CompactDictionary<object, CausalTimestamp>(0, edgeComparer)
        );
    }
    
    private void InitializeReplicatedTreeMetadata(CrdtMetadata metadata, string propertyPath, object propertyValue, ICrdtMetadataState boxedCausalTimestamp)
    {
        var typeInfo = PocoPathHelper.GetTypeInfo(propertyValue.GetType(), crdtContexts);
        if (!typeInfo.Properties.TryGetValue("Nodes", out var nodesProp) || !nodesProp.CanRead) return;

        if (nodesProp.Getter!(propertyValue) is not IDictionary nodesDictionary) return;

        var idComparer = elementComparerProvider.GetComparer(typeof(object));

        var adds = new CompactDictionary<object, ISet<Guid>>(nodesDictionary.Count, idComparer);
        foreach (var key in nodesDictionary.Keys)
        {
            if (key is null) continue;

            adds[key] = new CompactSet<Guid>(1) { Guid.NewGuid() };
            
            var nodeIdString = GetVoterKey(key);
            metadata.States[pool.GetOrAdd($"{propertyPath}.Nodes.['{nodeIdString}'].Value")] = boxedCausalTimestamp;
            metadata.States[pool.GetOrAdd($"{propertyPath}.Nodes.['{nodeIdString}'].ParentId")] = boxedCausalTimestamp;
        }

        metadata.States[propertyPath] = new OrSetState(
            Adds: adds,
            Removes: new CompactDictionary<object, IDictionary<Guid, CausalTimestamp>>(0, idComparer)
        );
    }

    private static void TrimMetadataCollections(CrdtMetadata metadata)
    {
        if (metadata.States is CompactDictionary<string, ICrdtMetadataState> compactStates)
        {
            compactStates.TrimExcess();
        }

        if (metadata.VersionVector is CompactDictionary<string, long> compactVv)
        {
            compactVv.TrimExcess();
        }

        if (metadata.SeenExceptions is CompactSet<CrdtOperation> compactExceptions)
        {
            compactExceptions.TrimExcess();
        }

        foreach (var state in metadata.States.Values)
        {
            switch (state)
            {
                case LwwMapState { Keys: CompactDictionary<object, CausalTimestamp> lwwMapKeys }:
                    lwwMapKeys.TrimExcess();
                    break;
                case FwwMapState { Keys: CompactDictionary<object, CausalTimestamp> fwwMapKeys }:
                    fwwMapKeys.TrimExcess();
                    break;
                case LwwSetState lwwSet:
                    if (lwwSet.Adds is CompactDictionary<object, CrdtTimestamp> lwwAdds) lwwAdds.TrimExcess();
                    if (lwwSet.Removes is CompactDictionary<object, CausalTimestamp> lwwRemoves) lwwRemoves.TrimExcess();
                    break;
                case FwwSetState fwwSet:
                    if (fwwSet.Adds is CompactDictionary<object, CrdtTimestamp> fwwAdds) fwwAdds.TrimExcess();
                    if (fwwSet.Removes is CompactDictionary<object, CausalTimestamp> fwwRemoves) fwwRemoves.TrimExcess();
                    break;
                case TwoPhaseSetState twoPhaseSet:
                    if (twoPhaseSet.Adds is CompactSet<object> tpAdds) tpAdds.TrimExcess();
                    if (twoPhaseSet.Tombstones is CompactDictionary<object, CausalTimestamp> tpTombstones) tpTombstones.TrimExcess();
                    break;
                case OrSetState orSet:
                    if (orSet.Adds is CompactDictionary<object, ISet<Guid>> orAdds)
                    {
                        orAdds.TrimExcess();
                        foreach (var tagSet in orAdds.Values)
                        {
                            if (tagSet is CompactSet<Guid> compactTags) compactTags.TrimExcess();
                        }
                    }

                    if (orSet.Removes is CompactDictionary<object, IDictionary<Guid, CausalTimestamp>> orRemoves)
                    {
                        orRemoves.TrimExcess();
                        foreach (var tagMap in orRemoves.Values)
                        {
                            if (tagMap is CompactDictionary<Guid, CausalTimestamp> compactTagMap) compactTagMap.TrimExcess();
                        }
                    }
                    break;
                case TwoPhaseGraphState graphState:
                    if (graphState.VertexAdds is CompactSet<object> vAdds) vAdds.TrimExcess();
                    if (graphState.VertexTombstones is CompactDictionary<object, CausalTimestamp> vTombstones) vTombstones.TrimExcess();
                    if (graphState.EdgeAdds is CompactSet<object> eAdds) eAdds.TrimExcess();
                    if (graphState.EdgeTombstones is CompactDictionary<object, CausalTimestamp> eTombstones) eTombstones.TrimExcess();
                    break;
                case CounterState { Contributions: CompactDictionary<string, PnCounterState> counterContribs }:
                    counterContribs.TrimExcess();
                    break;
                case GCounterState { Contributions: CompactDictionary<string, decimal> gCounterContribs }:
                    gCounterContribs.TrimExcess();
                    break;
                case CounterMapState { Keys: CompactDictionary<object, PnCounterState> counterMapKeys }:
                    counterMapKeys.TrimExcess();
                    break;
                case AverageRegisterState { Contributions: CompactDictionary<string, AverageRegisterValue> avgContribs }:
                    avgContribs.TrimExcess();
                    break;
                case QuorumState { Approvals: CompactDictionary<object, ISet<string>> approvals }:
                    approvals.TrimExcess();
                    foreach (var voterSet in approvals.Values)
                    {
                        if (voterSet is CompactSet<string> compactVoters) compactVoters.TrimExcess();
                    }
                    break;
            }
        }
    }

    private string GetVoterKey(object voter)
    {
        if (voter is string s) return s;
        var bytes = crdtSerializer.SerializeToBytes(voter, voter.GetType());
        return Convert.ToBase64String(bytes);
    }
}