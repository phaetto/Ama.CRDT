namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly remove a key and its associated value from a dictionary or map.
/// </summary>
/// <param name="Key">The key of the dictionary entry to remove.</param>
public readonly record struct MapRemoveIntent(object Key) : IOperationIntent;