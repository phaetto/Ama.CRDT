using Ama.CRDT.Models.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ama.CRDT.Models;

/// <summary>
/// A record struct that holds information about an exclusive lock on a property.
/// </summary>
/// <param name="LockHolderId">The identifier of the entity holding the lock.</param>
/// <param name="Timestamp">The timestamp when the lock was acquired.</param>
public sealed record LockInfo(string LockHolderId, ICrdtTimestamp Timestamp);

/// <summary>
/// Encapsulates the state required for conflict resolution, such as LWW timestamps and version vectors,
/// externalizing it from the data model itself.
/// </summary>
public sealed class CrdtMetadata
{
    /// <summary>
    /// Gets a custom <see cref="IJsonTypeInfoResolver"/> for this type that enables
    /// efficient serialization by omitting empty collections.
    /// </summary>
    /// <example>
    /// <code>
    /// var options = new JsonSerializerOptions { TypeInfoResolver = CrdtMetadata.JsonResolver };
    /// var json = JsonSerializer.Serialize(metadata, options);
    /// </code>
    /// </example>
    public static IJsonTypeInfoResolver JsonResolver { get; } = new CrdtMetadataJsonResolver();

    /// <summary>
    /// Gets a dictionary that stores the last-seen timestamp for properties managed by the Last-Writer-Wins (LWW) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> Lww { get; } = new Dictionary<string, ICrdtTimestamp>();

    /// <summary>
    /// Gets a version vector mapping a ReplicaId to the latest contiguous timestamp received from that replica.
    /// Operations with timestamps less than or equal to this value are considered seen and are ignored.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> VersionVector { get; } = new Dictionary<string, ICrdtTimestamp>();

    /// <summary>
    /// Gets a set of operations that have been received out of order.
    /// This set is used for idempotency checks and can be compacted once the version vector advances.
    /// </summary>
    public ISet<CrdtOperation> SeenExceptions { get; } = new HashSet<CrdtOperation>();
    
    /// <summary>
    /// Gets a dictionary that stores the ordered list of positional identifiers for properties managed by the ArrayLcsStrategy.
    /// The key is the JSON Path to the array property.
    /// </summary>
    public IDictionary<string, List<PositionalIdentifier>> PositionalTrackers { get; } = new Dictionary<string, List<PositionalIdentifier>>();

    /// <summary>
    /// Gets a dictionary that stores the per-replica contributions for properties managed by the AverageRegisterStrategy.
    /// The outer key is the JSON Path to the property, the inner key is the ReplicaId.
    /// </summary>
    public IDictionary<string, IDictionary<string, AverageRegisterValue>> AverageRegisters { get; } = new Dictionary<string, IDictionary<string, AverageRegisterValue>>();
    
    /// <summary>
    /// Gets a dictionary that stores the state for properties managed by the Two-Phase Set (2P-Set) strategy.
    /// The key is the JSON Path to the property. The value tuple contains an 'Adds' set and a 'Tombstones' set.
    /// </summary>
    public IDictionary<string, (ISet<object> Adds, ISet<object> Tomstones)> TwoPhaseSets { get; } = new Dictionary<string, (ISet<object>, ISet<object>)>();

    /// <summary>
    /// Gets a dictionary that stores the state for properties managed by the Last-Writer-Wins Set (LWW-Set) strategy.
    /// The key is the JSON Path to the property. The value tuple contains dictionaries for 'Adds' and 'Removes' with their associated timestamps.
    /// </summary>
    public IDictionary<string, (IDictionary<object, ICrdtTimestamp> Adds, IDictionary<object, ICrdtTimestamp> Removes)> LwwSets { get; } = new Dictionary<string, (IDictionary<object, ICrdtTimestamp>, IDictionary<object, ICrdtTimestamp>)>();

    /// <summary>
    /// Gets a dictionary that stores the state for properties managed by the Observed-Remove Set (OR-Set) strategy.
    /// The key is the JSON Path to the property. The value tuple contains dictionaries mapping an element to a set of unique tags for both 'Adds' and 'Removes'.
    /// </summary>
    public IDictionary<string, (IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes)> OrSets { get; } = new Dictionary<string, (IDictionary<object, ISet<Guid>>, IDictionary<object, ISet<Guid>>)>();

    /// <summary>
    /// Gets a dictionary that stores the state for properties managed by the Priority Queue strategy.
    /// It functions similarly to an LWW-Set, tracking additions and removals with timestamps.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, (IDictionary<object, ICrdtTimestamp> Adds, IDictionary<object, ICrdtTimestamp> Removes)> PriorityQueues { get; } = new Dictionary<string, (IDictionary<object, ICrdtTimestamp>, IDictionary<object, ICrdtTimestamp>)>();
        
    /// <summary>
    /// Gets a dictionary that stores the state for properties managed by the LSEQ strategy.
    /// The key is the JSON Path to the array. The value is a list of items, each pairing a dense identifier with a value.
    /// </summary>
    public IDictionary<string, List<LseqItem>> LseqTrackers { get; } = new Dictionary<string, List<LseqItem>>();
    
    /// <summary>
    /// Gets a dictionary that stores the state for properties managed by the Exclusive Lock strategy.
    /// The key is the JSON path to the locked property. A null value indicates the lock is released.
    /// </summary>
    public IDictionary<string, LockInfo?> ExclusiveLocks { get; } = new Dictionary<string, LockInfo?>();
}