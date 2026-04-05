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
    /// Gets or sets a dictionary that maps a JSON Path of a property to its specific CRDT resolution state.
    /// This utilizes polymorphic serialization for all concrete state implementations (e.g., LwwSetState, PnCounterState).
    /// </summary>
    public IDictionary<string, ICrdtMetadataState> States { get; set; } = new Dictionary<string, ICrdtMetadataState>();

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
    /// Creates a deep copy of this metadata instance.
    /// </summary>
    /// <returns>A new <see cref="CrdtMetadata"/> object that is a copy of this instance.</returns>
    public CrdtMetadata DeepClone()
    {
        var newMetadata = new CrdtMetadata();

        foreach (var kvp in States)
        {
            newMetadata.States.Add(kvp.Key, kvp.Value.DeepClone());
        }

        foreach (var kvp in VersionVector) 
        { 
            newMetadata.VersionVector.Add(kvp.Key, kvp.Value); 
        }

        foreach (var op in SeenExceptions) 
        { 
            newMetadata.SeenExceptions.Add(op); 
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
            foreach (var kvp in metadata.States)
            {
                if (merged.States.TryGetValue(kvp.Key, out var existing))
                {
                    merged.States[kvp.Key] = existing.Merge(kvp.Value);
                }
                else
                {
                    merged.States[kvp.Key] = kvp.Value.DeepClone();
                }
            }

            foreach (var (replicaId, clock) in metadata.VersionVector)
            {
                if (!merged.VersionVector.TryGetValue(replicaId, out var existingClock) || clock > existingClock)
                {
                    merged.VersionVector[replicaId] = clock;
                }
            }

            foreach (var op in metadata.SeenExceptions)
            {
                merged.SeenExceptions.Add(op);
            }
        }

        return merged;
    }

    /// <inheritdoc />
    public bool Equals(CrdtMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return DictionaryEquals(States, other.States)
            && DictionaryEquals(VersionVector, other.VersionVector)
            && SeenExceptions.SetEquals(other.SeenExceptions);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetDictionaryHashCode(States));
        hash.Add(GetDictionaryHashCode(VersionVector));
        hash.Add(GetSetHashCode(SeenExceptions));
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

    private static int GetSetHashCode<T>(ISet<T> set)
    {
        if (set is null) return 0;
        int hash = 0;
        foreach (var item in set)
        {
            hash ^= item?.GetHashCode() ?? 0;
        }
        return hash;
    }
}