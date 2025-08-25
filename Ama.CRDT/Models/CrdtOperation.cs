namespace Ama.CRDT.Models;

/// <summary>
/// Represents a single, atomic CRDT operation within a patch.
/// </summary>
/// <param name="Id">A unique identifier for this specific operation instance, used for tie-breaking and idempotency.</param>
/// <param name="ReplicaId">The identifier for the source replica that generated this operation.</param>
/// <param name="JsonPath">The JSON Path to the target property within the document.</param>
/// <param name="Type">The type of operation to perform (e.g., Upsert, Remove, Increment).</param>
/// <param name="Value">The value to be used in the operation (e.g., the new property value, the amount to increment by).</param>
/// <param name="Timestamp">The logical timestamp of the operation, used for conflict resolution.</param>
public readonly record struct CrdtOperation(Guid Id, string ReplicaId, string JsonPath, OperationType Type, object? Value, ICrdtTimestamp Timestamp);