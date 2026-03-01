namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly insert a value into an ordered sequence or collection at a specific index.
/// </summary>
/// <param name="Index">The zero-based position in the sequence where the value should be inserted.</param>
/// <param name="Value">The value to insert.</param>
public readonly record struct InsertIntent(int Index, object? Value) : IOperationIntent;