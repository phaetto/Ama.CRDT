namespace Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using System.Collections;


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
    /// Gets or sets a dictionary that stores the last-seen timestamp for properties managed by the Last-Writer-Wins (LWW) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> Lww { get; set; } = new Dictionary<string, ICrdtTimestamp>();

    /// <summary>
    /// Gets or sets a version vector mapping a ReplicaId to the latest contiguous timestamp received from that replica.
    /// Operations with timestamps less than or equal to this value are considered seen and are ignored.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> VersionVector { get; set; } = new Dictionary<string, ICrdtTimestamp>();

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
    /// Gets or sets a dictionary that stores the state for properties managed by the Exclusive Lock strategy.
    /// The key is the JSON path to the locked property. A null value indicates the lock is released.
    /// </summary>
    public IDictionary<string, LockInfo?> ExclusiveLocks { get; set; } = new Dictionary<string, LockInfo?>();

    /// <summary>
    /// Gets or sets a dictionary that stores the state for properties managed by the Last-Writer-Wins Map (LWW-Map) strategy.
    /// The outer key is the JSON Path to the property. The inner dictionary maps each key from the user's dictionary to its LWW timestamp.
    /// </summary>
    public IDictionary<string, IDictionary<object, ICrdtTimestamp>> LwwMaps { get; set; } = new Dictionary<string, IDictionary<object, ICrdtTimestamp>>();

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

    /// <inheritdoc />
    public bool Equals(CrdtMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return DictionaryEquals(Lww, other.Lww)
            && DictionaryEquals(VersionVector, other.VersionVector)
            && SeenExceptions.SetEquals(other.SeenExceptions)
            && DictionaryOfListsEquals(PositionalTrackers, other.PositionalTrackers)
            && DictionaryOfDictionariesEquals(AverageRegisters, other.AverageRegisters)
            && DictionaryEquals(TwoPhaseSets, other.TwoPhaseSets)
            && DictionaryEquals(LwwSets, other.LwwSets)
            && DictionaryEquals(OrSets, other.OrSets)
            && DictionaryEquals(PriorityQueues, other.PriorityQueues)
            && DictionaryOfListsEquals(LseqTrackers, other.LseqTrackers)
            && DictionaryEquals(ExclusiveLocks, other.ExclusiveLocks)
            && DictionaryOfDictionariesEquals(LwwMaps, other.LwwMaps)
            && DictionaryEquals(OrMaps, other.OrMaps)
            && DictionaryOfDictionariesEquals(CounterMaps, other.CounterMaps)
            && DictionaryEquals(TwoPhaseGraphs, other.TwoPhaseGraphs)
            && DictionaryEquals(ReplicatedTrees, other.ReplicatedTrees);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetDictionaryHashCode(Lww));
        hash.Add(GetDictionaryHashCode(VersionVector));
        hash.Add(GetSetHashCode(SeenExceptions));
        hash.Add(GetDictionaryOfListsHashCode(PositionalTrackers));
        hash.Add(GetDictionaryOfDictionariesHashCode(AverageRegisters));
        hash.Add(GetDictionaryHashCode(TwoPhaseSets));
        hash.Add(GetDictionaryHashCode(LwwSets));
        hash.Add(GetDictionaryHashCode(OrSets));
        hash.Add(GetDictionaryHashCode(PriorityQueues));
        hash.Add(GetDictionaryOfListsHashCode(LseqTrackers));
        hash.Add(GetDictionaryHashCode(ExclusiveLocks));
        hash.Add(GetDictionaryOfDictionariesHashCode(LwwMaps));
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