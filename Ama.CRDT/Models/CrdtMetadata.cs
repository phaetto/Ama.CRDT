namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Encapsulates the metadata state required for Conflict-free Replicated Data Type (CRDT) operations and resolution.
/// </summary>
/// <remarks>
/// By externalizing this state from the data model, your plain old CLR objects (POCOs) remain clean and unaware of 
/// underlying CRDT mechanics. This class holds the exact tracking data required to safely merge concurrent updates, 
/// handle out-of-order network delivery, and maintain idempotency across a distributed system.
/// <para>
/// For serialization, it is highly recommended to use the pre-configured options from the 
/// DI container (injected via <c>[FromKeyedServices("Ama.CRDT")] JsonSerializerOptions</c>), 
/// which automatically applies modifiers for compact and polymorphic serialization.
/// </para>
/// </remarks>
/// <example>
/// The following example demonstrates initializing a document's metadata and serializing it:
/// <code>
/// // 1. Create your clean POCO model
/// var myModel = new User { Id = 1, Name = "Alice" };
/// 
/// // 2. Initialize the CRDT metadata tracking state for this model
/// var metadata = metadataManager.Initialize(myModel);
/// 
/// // 3. Serialize to JSON compactly for storage or network transmission using DI options
/// string json = JsonSerializer.Serialize(metadata, injectedJsonOptions);
/// </code>
/// </example>
public sealed record CrdtMetadata : IEquatable<CrdtMetadata>
{
    /// <summary>
    /// Gets or sets a dictionary that maps a JSON Path of a property to its specific CRDT resolution state.
    /// </summary>
    /// <remarks>
    /// The path is represented using standard JSON Path notation (e.g., <c>$.user.name</c> or <c>$.tags[0]</c>).
    /// The mapped state utilizes polymorphic serialization for all concrete state implementations. For example, 
    /// a property using <c>LwwStrategy</c> will map to a <see cref="CausalTimestamp"/>, while an <c>OrSetStrategy</c> 
    /// property will map to an <see cref="OrSetState"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example dictionary contents:
    /// // ["$.Name"] = new CausalTimestamp(1690000000, "replica-A", 1)
    /// // ["$.Roles"] = new OrSetState(...)
    /// </code>
    /// </example>
    public IDictionary<string, ICrdtMetadataState> States { get; set; } = new Dictionary<string, ICrdtMetadataState>();

    /// <summary>
    /// Gets or sets a version vector mapping a unique Replica ID to the latest contiguous causal sequence clock received from that replica.
    /// </summary>
    /// <remarks>
    /// This acts as the baseline for idempotency. Any incoming operations with a clock less than or equal to the 
    /// stored value for that replica are considered already applied (or obsolete) and can be safely ignored.
    /// </remarks>
    /// <example>
    /// <code>
    /// // "replica-A" has processed all events up to logical clock 5 without gaps.
    /// // VersionVector["replica-A"] = 5;
    /// </code>
    /// </example>
    public IDictionary<string, long> VersionVector { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets or sets a set of operations that have been received out of order (gaps in the causal history).
    /// </summary>
    /// <remarks>
    /// When an operation arrives with a sequence clock higher than the expected next contiguous clock in the 
    /// <see cref="VersionVector"/>, it is stored here as an exception (also known as a "dot" in Dotted Version Vectors).
    /// This ensures operations can be applied immediately even if previous operations are delayed. Once the missing 
    /// operations arrive, the version vector advances and these stored exceptions are automatically compacted and cleared.
    /// </remarks>
    public ISet<CrdtOperation> SeenExceptions { get; set; } = new HashSet<CrdtOperation>();

    /// <summary>
    /// Creates a deep copy of this metadata instance, ensuring all underlying states, version vectors, 
    /// and seen exceptions are independently cloned.
    /// </summary>
    /// <returns>A new <see cref="CrdtMetadata"/> object that is an exact, unlinked copy of this instance.</returns>
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
    /// <remarks>
    /// This method is crucial when merging states from different replicas or partitions. It resolves conflicts 
    /// by combining version vectors (taking the maximum contiguous clock for each replica), merging individual property 
    /// states based on their specific strategy rules, and aggregating any seen exceptions.
    /// </remarks>
    /// <param name="metadatas">The metadata instances to merge.</param>
    /// <returns>A new <see cref="CrdtMetadata"/> containing the fully merged state.</returns>
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