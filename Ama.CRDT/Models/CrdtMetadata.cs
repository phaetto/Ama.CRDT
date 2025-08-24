namespace Ama.CRDT.Models;

/// <summary>
/// Encapsulates the state required for conflict resolution, such as LWW timestamps and version vectors,
/// externalizing it from the data model itself.
/// </summary>
public sealed class CrdtMetadata
{
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
}