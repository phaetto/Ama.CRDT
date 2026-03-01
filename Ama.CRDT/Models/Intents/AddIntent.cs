namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly add an item to an unordered collection or set.
/// </summary>
/// <param name="Value">The value to add.</param>
public readonly record struct AddIntent(object? Value) : IOperationIntent;