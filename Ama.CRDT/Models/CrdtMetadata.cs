namespace Ama.CRDT.Models;

using Ama.CRDT.Models.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Encapsulates the state required for conflict resolution, such as LWW timestamps and version vectors,
/// externalizing it from the data model itself.
/// <para>
/// For serialization, use the pre-configured options from <see cref="CrdtJsonContext"/>.
/// Use <see cref="CrdtJsonContext.MetadataCompactOptions"/> for an efficient, compact JSON output,
/// or <see cref="CrdtJsonContext.DefaultOptions"/> for standard serialization.
/// </para>
/// </summary>
public sealed record CrdtMetadata : IEquatable<CrdtMetadata>
{
    /// <summary>
    /// Gets or sets a dictionary that stores the epoch generation for properties managed by the Epoch Bound strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, int> Epochs { get; set; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets or sets a dictionary that stores the last-seen timestamp for properties managed by the Last-Writer-Wins (LWW) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> Lww { get; set; } = new Dictionary<string, ICrdtTimestamp>();

    /// <summary>
    /// Gets or sets a dictionary that stores the earliest-seen timestamp for properties managed by the First-Writer-Wins (FWW) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> Fww { get; set; } = new Dictionary<string, ICrdtTimestamp>();

    /// <summary>
    /// Gets or sets a version vector mapping a ReplicaId to the latest contiguous causal sequence clock received from that replica.
    /// Operations with clocks less than or equal to this value are considered seen and are ignored.
    /// </summary>
    public IDictionary<string, long> VersionVector { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets or sets a set of operations that have been received out of order.
    /// This set is used for idempotency checks and can be compacted once the version vector advances.
    /// </summary>
    public ISet<CrdtOperation> SeenExceptions { get; set; } = new HashSet<CrdtOperation>();
    
    /// <summary>
    /// Gets or sets a dictionary that stores the ordered list of positional identifiers for properties managed by the ArrayLcsStrategy.
    /// The key is the JSON Path to the array property.
    /// </summary>
    public IDictionary<string, List<PositionalIdentifier>> PositionalTrackers { get; set; } = new Dictionary<string, List<PositionalIdentifier>>();

    /// <summary>
    /// Gets or sets a dictionary that stores the per-replica contributions for properties managed by the AverageRegisterStrategy.
    /// The outer key is the JSON Path to the property, the inner key is the ReplicaId.
    /// </summary>
    public IDictionary<string, IDictionary<string, AverageRegisterValue>> AverageRegisters { get; set; } = new Dictionary<string, IDictionary<string, AverageRegisterValue>>();
    
    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Two-Phase Set (2P-Set) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, TwoPhaseSetState> TwoPhaseSets { get; set; } = new Dictionary<string, TwoPhaseSetState>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Last-Writer-Wins Set (LWW-Set) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, LwwSetState> LwwSets { get; set; } = new Dictionary<string, LwwSetState>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the First-Writer-Wins Set (FWW-Set) strategy.
    /// The key is the JSON Path to the property. Uses LwwSetState as the state structure is identical.
    /// </summary>
    public IDictionary<string, LwwSetState> FwwSets { get; set; } = new Dictionary<string, LwwSetState>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Observed-Remove Set (OR-Set) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, OrSetState> OrSets { get; set; } = new Dictionary<string, OrSetState>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Priority Queue strategy.
    /// It functions similarly to an LWW-Set, tracking additions and removals with timestamps.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, LwwSetState> PriorityQueues { get; set; } = new Dictionary<string, LwwSetState>();
        
    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the LSEQ strategy.
    /// The key is the JSON Path to the array. The value is a list of items, each pairing a dense identifier with a value.
    /// </summary>
    public IDictionary<string, List<LseqItem>> LseqTrackers { get; set; } = new Dictionary<string, List<LseqItem>>();
    
    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the RGA (Replicated Growable Array) strategy.
    /// The key is the JSON Path to the array. The value is a list of RGA nodes tracking insertion causality and tombstones.
    /// </summary>
    public IDictionary<string, List<RgaItem>> RgaTrackers { get; set; } = new Dictionary<string, List<RgaItem>>();
    
    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Last-Writer-Wins Map (LWW-Map) strategy.
    /// The outer key is the JSON Path to the property. The inner dictionary maps each key from the user's dictionary to its LWW timestamp.
    /// </summary>
    public IDictionary<string, IDictionary<object, ICrdtTimestamp>> LwwMaps { get; set; } = new Dictionary<string, IDictionary<object, ICrdtTimestamp>>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the First-Writer-Wins Map (FWW-Map) strategy.
    /// The outer key is the JSON Path to the property. The inner dictionary maps each key from the user's dictionary to its earliest-seen timestamp.
    /// </summary>
    public IDictionary<string, IDictionary<object, ICrdtTimestamp>> FwwMaps { get; set; } = new Dictionary<string, IDictionary<object, ICrdtTimestamp>>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Observed-Remove Map (OR-Map) strategy.
    /// The key is the JSON Path to the property. The value contains dictionaries for 'Adds' and 'Removes' with their associated unique tags, managing key presence.
    /// Value updates are managed separately using LWW timestamps in the main Lww dictionary.
    /// </summary>
    public IDictionary<string, OrSetState> OrMaps { get; set; } = new Dictionary<string, OrSetState>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Counter Map strategy.
    /// The outer key is the JSON Path to the property. The inner dictionary maps each key from the user's dictionary
    /// to its PN-Counter state.
    /// </summary>
    public IDictionary<string, IDictionary<object, PnCounterState>> CounterMaps { get; set; } = new Dictionary<string, IDictionary<object, PnCounterState>>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Two-Phase Graph (2P-Graph) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, TwoPhaseGraphState> TwoPhaseGraphs { get; set; } = new Dictionary<string, TwoPhaseGraphState>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for node existence in properties managed by the Replicated Tree strategy.
    /// It uses OR-Set logic, mapping a node's ID to unique tags for its additions and removals.
    /// </summary>
    public IDictionary<string, OrSetState> ReplicatedTrees { get; set; } = new Dictionary<string, OrSetState>();

    /// <summary>
    /// Creates a deep copy of this metadata instance.
    /// </summary>
    /// <returns>A new <see cref="CrdtMetadata"/> object that is a copy of this instance.</returns>
    public CrdtMetadata DeepClone()
    {
        var newMetadata = new CrdtMetadata();

        foreach (var kvp in Epochs) { newMetadata.Epochs.Add(kvp.Key, kvp.Value); }
        foreach (var kvp in Lww) { newMetadata.Lww.Add(kvp.Key, kvp.Value); }
        foreach (var kvp in Fww) { newMetadata.Fww.Add(kvp.Key, kvp.Value); }
        foreach (var kvp in PositionalTrackers) { newMetadata.PositionalTrackers.Add(kvp.Key, new List<PositionalIdentifier>(kvp.Value)); }
        foreach (var kvp in AverageRegisters) { newMetadata.AverageRegisters.Add(kvp.Key, new Dictionary<string, AverageRegisterValue>(kvp.Value)); }

        foreach (var kvp in TwoPhaseSets)
        {
            newMetadata.TwoPhaseSets.Add(kvp.Key, new TwoPhaseSetState(
                Adds: new HashSet<object>(kvp.Value.Adds, (kvp.Value.Adds as HashSet<object>)?.Comparer),
                Tomstones: new HashSet<object>(kvp.Value.Tomstones, (kvp.Value.Tomstones as HashSet<object>)?.Comparer)
            ));
        }

        foreach (var kvp in LwwSets)
        {
            newMetadata.LwwSets.Add(kvp.Key, new LwwSetState(
                Adds: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Adds, (kvp.Value.Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer),
                Removes: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Removes, (kvp.Value.Removes as Dictionary<object, ICrdtTimestamp>)?.Comparer)
            ));
        }

        foreach (var kvp in FwwSets)
        {
            newMetadata.FwwSets.Add(kvp.Key, new LwwSetState(
                Adds: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Adds, (kvp.Value.Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer),
                Removes: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Removes, (kvp.Value.Removes as Dictionary<object, ICrdtTimestamp>)?.Comparer)
            ));
        }

        foreach (var kvp in OrSets)
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

        foreach (var kvp in PriorityQueues)
        {
            newMetadata.PriorityQueues.Add(kvp.Key, new LwwSetState(
                Adds: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Adds, (kvp.Value.Adds as Dictionary<object, ICrdtTimestamp>)?.Comparer),
                Removes: new Dictionary<object, ICrdtTimestamp>(kvp.Value.Removes, (kvp.Value.Removes as Dictionary<object, ICrdtTimestamp>)?.Comparer)
            ));
        }

        foreach (var kvp in LseqTrackers) { newMetadata.LseqTrackers.Add(kvp.Key, new List<LseqItem>(kvp.Value)); }
        foreach (var kvp in RgaTrackers) { newMetadata.RgaTrackers.Add(kvp.Key, new List<RgaItem>(kvp.Value)); }
        foreach (var kvp in VersionVector) { newMetadata.VersionVector.Add(kvp.Key, kvp.Value); }
        foreach (var op in SeenExceptions) { newMetadata.SeenExceptions.Add(op); }

        foreach (var kvp in LwwMaps)
        {
            newMetadata.LwwMaps.Add(kvp.Key, new Dictionary<object, ICrdtTimestamp>(kvp.Value, (kvp.Value as Dictionary<object, ICrdtTimestamp>)?.Comparer));
        }

        foreach (var kvp in FwwMaps)
        {
            newMetadata.FwwMaps.Add(kvp.Key, new Dictionary<object, ICrdtTimestamp>(kvp.Value, (kvp.Value as Dictionary<object, ICrdtTimestamp>)?.Comparer));
        }

        foreach (var kvp in OrMaps)
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

        foreach (var kvp in CounterMaps)
        {
            newMetadata.CounterMaps.Add(kvp.Key, new Dictionary<object, PnCounterState>(kvp.Value, (kvp.Value as Dictionary<object, PnCounterState>)?.Comparer));
        }
        
        foreach (var kvp in TwoPhaseGraphs)
        {
            newMetadata.TwoPhaseGraphs.Add(kvp.Key, new TwoPhaseGraphState(
                VertexAdds: new HashSet<object>(kvp.Value.VertexAdds, (kvp.Value.VertexAdds as HashSet<object>)?.Comparer),
                VertexTombstones: new HashSet<object>(kvp.Value.VertexTombstones, (kvp.Value.VertexTombstones as HashSet<object>)?.Comparer),
                EdgeAdds: new HashSet<object>(kvp.Value.EdgeAdds, (kvp.Value.EdgeAdds as HashSet<object>)?.Comparer),
                EdgeTombstones: new HashSet<object>(kvp.Value.EdgeTombstones, (kvp.Value.EdgeTombstones as HashSet<object>)?.Comparer)
            ));
        }

        foreach (var kvp in ReplicatedTrees)
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

    /// <summary>
    /// Merges multiple <see cref="CrdtMetadata"/> instances into a single, unified state.
    /// </summary>
    /// <param name="metadatas">The metadata instances to merge.</param>
    /// <returns>A new <see cref="CrdtMetadata"/> containing the merged state.</returns>
    public static CrdtMetadata Merge(params CrdtMetadata[] metadatas)
    {
        ArgumentNullException.ThrowIfNull(metadatas);

        if (metadatas.Length == 0)
        {
            return new CrdtMetadata();
        }

        if (metadatas.Length == 1)
        {
            return metadatas[0].DeepClone();
        }

        var merged = new CrdtMetadata();

        foreach (var metadata in metadatas.Where(m => m is not null))
        {
            foreach (var kvp in metadata.Epochs) 
            {
                if (!merged.Epochs.TryGetValue(kvp.Key, out var existingEpoch) || kvp.Value > existingEpoch)
                {
                    merged.Epochs[kvp.Key] = kvp.Value;
                }
            }
            
            foreach (var kvp in metadata.Lww) 
            {
                if (!merged.Lww.TryGetValue(kvp.Key, out var existingTs) || kvp.Value.CompareTo(existingTs) > 0)
                {
                    merged.Lww[kvp.Key] = kvp.Value;
                }
            }
            
            foreach (var kvp in metadata.Fww) 
            {
                if (!merged.Fww.TryGetValue(kvp.Key, out var existingTs) || kvp.Value.CompareTo(existingTs) < 0)
                {
                    merged.Fww[kvp.Key] = kvp.Value;
                }
            }
            
            foreach (var kvp in metadata.TwoPhaseSets) merged.TwoPhaseSets[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.LwwSets) merged.LwwSets[kvp.Key] = kvp.Value;
            foreach (var kvp in metadata.FwwSets) merged.FwwSets[kvp.Key] = kvp.Value;
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
            foreach (var kvp in metadata.FwwMaps) merged.FwwMaps[kvp.Key] = new Dictionary<object, ICrdtTimestamp>(kvp.Value, (kvp.Value as Dictionary<object, ICrdtTimestamp>)?.Comparer);
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

    /// <inheritdoc />
    public bool Equals(CrdtMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return DictionaryEquals(Epochs, other.Epochs)
            && DictionaryEquals(Lww, other.Lww)
            && DictionaryEquals(Fww, other.Fww)
            && DictionaryEquals(VersionVector, other.VersionVector)
            && SeenExceptions.SetEquals(other.SeenExceptions)
            && DictionaryOfListsEquals(PositionalTrackers, other.PositionalTrackers)
            && DictionaryOfDictionariesEquals(AverageRegisters, other.AverageRegisters)
            && DictionaryEquals(TwoPhaseSets, other.TwoPhaseSets)
            && DictionaryEquals(LwwSets, other.LwwSets)
            && DictionaryEquals(FwwSets, other.FwwSets)
            && DictionaryEquals(OrSets, other.OrSets)
            && DictionaryEquals(PriorityQueues, other.PriorityQueues)
            && DictionaryOfListsEquals(LseqTrackers, other.LseqTrackers)
            && DictionaryOfListsEquals(RgaTrackers, other.RgaTrackers)
            && DictionaryOfDictionariesEquals(LwwMaps, other.LwwMaps)
            && DictionaryOfDictionariesEquals(FwwMaps, other.FwwMaps)
            && DictionaryEquals(OrMaps, other.OrMaps)
            && DictionaryOfDictionariesEquals(CounterMaps, other.CounterMaps)
            && DictionaryEquals(TwoPhaseGraphs, other.TwoPhaseGraphs)
            && DictionaryEquals(ReplicatedTrees, other.ReplicatedTrees);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetDictionaryHashCode(Epochs));
        hash.Add(GetDictionaryHashCode(Lww));
        hash.Add(GetDictionaryHashCode(Fww));
        hash.Add(GetDictionaryHashCode(VersionVector));
        hash.Add(GetSetHashCode(SeenExceptions));
        hash.Add(GetDictionaryOfListsHashCode(PositionalTrackers));
        hash.Add(GetDictionaryOfDictionariesHashCode(AverageRegisters));
        hash.Add(GetDictionaryHashCode(TwoPhaseSets));
        hash.Add(GetDictionaryHashCode(LwwSets));
        hash.Add(GetDictionaryHashCode(FwwSets));
        hash.Add(GetDictionaryHashCode(OrSets));
        hash.Add(GetDictionaryHashCode(PriorityQueues));
        hash.Add(GetDictionaryOfListsHashCode(LseqTrackers));
        hash.Add(GetDictionaryOfListsHashCode(RgaTrackers));
        hash.Add(GetDictionaryOfDictionariesHashCode(LwwMaps));
        hash.Add(GetDictionaryOfDictionariesHashCode(FwwMaps));
        hash.Add(GetDictionaryHashCode(OrMaps));
        hash.Add(GetDictionaryOfDictionariesHashCode(CounterMaps));
        hash.Add(GetDictionaryHashCode(TwoPhaseGraphs));
        hash.Add(GetDictionaryHashCode(ReplicatedTrees));
        return hash.ToHashCode();
    }

    private static bool DictionaryEquals<TKey, TValue>(IDictionary<TKey, TValue> left, IDictionary<TKey, TValue> right) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue) || !EqualityComparer<TValue>.Default.Equals(value, rightValue))
                return false;
        }
        return true;
    }

    private static bool DictionaryOfDictionariesEquals<TKey1, TKey2, TValue>(IDictionary<TKey1, IDictionary<TKey2, TValue>> left, IDictionary<TKey1, IDictionary<TKey2, TValue>> right) where TKey1 : notnull where TKey2 : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue) || !DictionaryEquals(value, rightValue))
                return false;
        }
        return true;
    }

    private static bool DictionaryOfListsEquals<TKey, TValue>(IDictionary<TKey, List<TValue>> left, IDictionary<TKey, List<TValue>> right) where TKey : notnull
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue) || value is null || rightValue is null || !value.SequenceEqual(rightValue))
                return false;
        }
        return true;
    }

    private static int GetDictionaryHashCode<TKey, TValue>(IDictionary<TKey, TValue> dict) where TKey : notnull
    {
        if (dict is null) return 0;
        int hash = 0;
        foreach (var (key, value) in dict)
        {
            hash ^= HashCode.Combine(key, value);
        }
        return hash;
    }

    private static int GetDictionaryOfDictionariesHashCode<TKey1, TKey2, TValue>(IDictionary<TKey1, IDictionary<TKey2, TValue>> dict) where TKey1 : notnull where TKey2 : notnull
    {
        if (dict is null) return 0;
        int hash = 0;
        foreach (var (key, value) in dict)
        {
            hash ^= (key?.GetHashCode() ?? 0) ^ GetDictionaryHashCode(value);
        }
        return hash;
    }

    private static int GetDictionaryOfListsHashCode<TKey, TValue>(IDictionary<TKey, List<TValue>> dict) where TKey : notnull
    {
        if (dict is null) return 0;
        int hash = 0;
        foreach (var (key, value) in dict)
        {
            int listHash = 0;
            if (value is not null)
            {
                foreach(var item in value)
                {
                    listHash ^= item?.GetHashCode() ?? 0;
                }
            }
            hash ^= (key?.GetHashCode() ?? 0) ^ listHash;
        }
        return hash;
    }

    private static int GetSetHashCode<T>(ISet<T> set)
    {
        if (set is null) return 0;
        int hash = 0;
        foreach(var item in set)
        {
            hash ^= item?.GetHashCode() ?? 0;
        }
        return hash;
    }
}