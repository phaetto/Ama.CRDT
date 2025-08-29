namespace Ama.CRDT.Models;

/// <summary>
/// Defines the types of operations that can be included in a CRDT patch.
/// </summary>
public enum OperationType
{
    /// <summary>
    /// Represents an operation that adds or updates a value.
    /// </summary>
    Upsert,

    /// <summary>
    /// Represents an operation that removes a value.
    /// </summary>
    Remove,

    /// <summary>
    /// Represents an operation that increments a numeric value (for CRDT Counters).
    /// </summary>
    Increment,

    /// <summary>
    /// Represents an operation to move an item within a collection.
    /// </summary>
    Move
}