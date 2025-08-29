namespace Ama.CRDT.Models;

/// <summary>
/// A record struct that holds information about an exclusive lock on a property.
/// </summary>
/// <param name="LockHolderId">The identifier of the entity holding the lock.</param>
/// <param name="Timestamp">The timestamp when the lock was acquired.</param>
public sealed record LockInfo(string LockHolderId, ICrdtTimestamp Timestamp);