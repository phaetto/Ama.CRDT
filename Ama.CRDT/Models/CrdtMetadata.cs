namespace Ama.CRDT.Models;

/// <summary>
/// Encapsulates the state required for conflict resolution (LWW timestamps, seen operation IDs),
/// externalizing it from the data model.
/// </summary>
public sealed class CrdtMetadata
{
    /// <summary>
    /// Gets a dictionary that stores the last-seen timestamp for properties managed by the Last-Writer-Wins (LWW) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> Lww { get; } = new Dictionary<string, ICrdtTimestamp>();

    /// <summary>
    /// A version vector mapping a ReplicaId to the latest contiguous timestamp received from that replica.
    /// Operations with timestamps less than or equal to this value are considered seen and are ignored.
    /// </summary>
    public IDictionary<string, ICrdtTimestamp> VersionVector { get; } = new Dictionary<string, ICrdtTimestamp>();

    /// <summary>
    /// Stores operations that have been received out of order (i.e., their timestamp is newer than what's
    /// in the version vector for that replica). This set is used for idempotency checks and can be compacted
    /// once the version vector advances.
    /// </summary>
    public ISet<CrdtOperation> SeenExceptions { get; } = new HashSet<CrdtOperation>();
    
    /// <summary>
    /// Stores the ordered list of positional identifiers for properties managed by the PositionalArrayStrategy.
    /// The key is the JSON Path to the array property.
    /// </summary>
    public IDictionary<string, List<PositionalIdentifier>> PositionalTrackers { get; } = new Dictionary<string, List<PositionalIdentifier>>();
}