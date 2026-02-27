namespace Ama.CRDT.Models;

using System;
using Ama.CRDT.Models.Serialization;

/// <summary>
/// Represents a single, atomic CRDT operation within a patch.
/// <para>
/// For serialization, use the pre-configured options from <see cref="CrdtJsonContext.DefaultOptions"/>
/// to ensure that polymorphic payloads in the <see cref="Value"/> property are handled correctly.
/// </para>
/// </summary>
/// <param name="Id">A unique identifier for this specific operation instance, used for tie-breaking and idempotency.</param>
/// <param name="ReplicaId">The identifier for the source replica that generated this operation.</param>
/// <param name="JsonPath">The JSON Path to the target property within the document.</param>
/// <param name="Type">The type of operation to perform (e.g., Upsert, Remove, Increment).</param>
/// <param name="Value">The value to be used in the operation (e.g., the new property value, the amount to increment by).</param>
/// <param name="Timestamp">The wall-clock logical timestamp of the operation, used for LWW conflict resolution.</param>
/// <param name="Clock">The monotonically increasing causal sequence number for the originating replica. Defaulted to 0 for backwards compatibility in creation.</param>
public readonly record struct CrdtOperation(Guid Id, string ReplicaId, string JsonPath, OperationType Type, object? Value, ICrdtTimestamp Timestamp, long Clock = 0);