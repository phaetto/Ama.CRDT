namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly increment or decrement a numeric value or counter.
/// </summary>
/// <param name="Value">The amount by which to increment (or decrement, if negative).</param>
public readonly record struct IncrementIntent(object? Value) : IOperationIntent;