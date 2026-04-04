namespace Ama.CRDT.Models;

using System;

/// <summary>
/// Represents the polymorphic base interface for all CRDT metadata state structures.
/// This allows mapping property JSON Paths to an abstracted state implementation.
/// </summary>
public interface ICrdtMetadataState : IEquatable<ICrdtMetadataState>
{
    /// <summary>
    /// Creates a deep copy of the state to prevent unintended reference mutations.
    /// </summary>
    /// <returns>A cloned instance of the state.</returns>
    ICrdtMetadataState DeepClone();

    /// <summary>
    /// Merges the state with another state of the same type.
    /// </summary>
    /// <param name="other">The other state to merge with.</param>
    /// <returns>A new state containing the merged result.</returns>
    ICrdtMetadataState Merge(ICrdtMetadataState other);
}