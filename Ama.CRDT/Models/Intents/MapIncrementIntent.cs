namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly increment or decrement a numeric value for a specific key within a dictionary or map.
/// </summary>
/// <param name="Key">The key of the dictionary entry to increment.</param>
/// <param name="Value">The amount by which to increment (or decrement, if negative).</param>
public readonly record struct MapIncrementIntent(object Key, object? Value) : IOperationIntent;