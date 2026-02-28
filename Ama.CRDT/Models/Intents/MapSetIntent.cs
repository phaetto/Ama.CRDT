namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly set a value for a specific key within a dictionary or map.
/// </summary>
/// <param name="Key">The key of the dictionary entry to set.</param>
/// <param name="Value">The new value to set.</param>
public readonly record struct MapSetIntent(object Key, object? Value) : IOperationIntent;