namespace Ama.CRDT.Services.GarbageCollection;

using Ama.CRDT.Models;

/// <summary>
/// Represents the metadata payload of a tombstone or deleted item being evaluated for garbage collection.
/// Strategies populate this structure with whatever causal or temporal data they track for a specific item.
/// </summary>
/// <param name="Timestamp">The wall-clock timestamp of the deletion, typically used by LWW strategies.</param>
/// <param name="ReplicaId">The identifier of the replica that originated the deletion, used for causal tracking.</param>
/// <param name="Version">The causal sequence number of the deletion, used in conjunction with ReplicaId.</param>
public readonly record struct CompactionCandidate(
    ICrdtTimestamp? Timestamp = null,
    string? ReplicaId = null,
    long? Version = null
);