namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly clear a property, collection, or state, resetting it.
/// </summary>
public readonly record struct ClearIntent : IOperationIntent;