namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly set a value at a specific index within a collection or sequence.
/// </summary>
/// <param name="Index">The zero-based position in the sequence where the value should be set.</param>
/// <param name="Value">The new value to set.</param>
public readonly record struct SetIndexIntent(int Index, object? Value) : IOperationIntent;