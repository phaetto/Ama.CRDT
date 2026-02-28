namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly remove a specific value from a collection or set.
/// </summary>
/// <param name="Value">The value to remove.</param>
public readonly record struct RemoveValueIntent(object? Value) : IOperationIntent;