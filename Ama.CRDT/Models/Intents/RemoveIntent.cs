namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly remove an item from a collection or sequence.
/// </summary>
/// <param name="Index">The zero-based position in the sequence of the item to be removed.</param>
public readonly record struct RemoveIntent(int Index) : IOperationIntent;