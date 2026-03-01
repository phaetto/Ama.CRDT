namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly set a value for a property or register.
/// </summary>
/// <param name="Value">The new value to set.</param>
public readonly record struct SetIntent(object? Value) : IOperationIntent;