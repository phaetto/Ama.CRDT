namespace Modern.CRDT.Models;

/// <summary>
/// Encapsulates the state required for conflict resolution in a state-based CRDT system.
/// This object is managed by the client and passed through the API to ensure idempotency and correct merge outcomes.
/// </summary>
public sealed class CrdtMetadata
{
    /// <summary>
    /// Gets a dictionary that stores the last-seen timestamp for properties managed by the Last-Writer-Wins (LWW) strategy.
    /// The key is the JSON Path to the property.
    /// </summary>
    public IDictionary<string, long> LwwTimestamps { get; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets a dictionary that stores the unique timestamps of operations that have already been applied for a specific property path.
    /// This is used by non-LWW strategies (like Counter and ArrayLcs) to ensure that operations are only applied once (idempotency).
    /// The key is the JSON Path to the property (e.g., "$.tags", "$.likes"), and the value is a set of seen operation timestamps for that property.
    /// </summary>
    public IDictionary<string, ISet<long>> SeenOperationIds { get; } = new Dictionary<string, ISet<long>>();
}