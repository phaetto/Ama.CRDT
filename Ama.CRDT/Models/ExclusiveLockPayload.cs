namespace Ama.CRDT.Models;

/// <summary>
/// A data structure for the payload of an Exclusive Lock operation.
/// </summary>
/// <param name="Value">The actual value of the property.</param>
/// <param name="LockHolderId">The identifier of the entity holding the lock. A null value signifies a lock release.</param>
public readonly record struct ExclusiveLockPayload(object? Value, string? LockHolderId);