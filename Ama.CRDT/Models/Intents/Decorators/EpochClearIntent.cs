namespace Ama.CRDT.Models.Intents.Decorators;

using System;

/// <summary>
/// Represents the intent to explicitly clear the state within an Epoch-bound decorator, 
/// incrementing the epoch and effectively clearing all local data for that path.
/// This is separate from standard clear intents to avoid clashing with base strategies.
/// </summary>
public readonly record struct EpochClearIntent : IOperationIntent, IEquatable<EpochClearIntent>
{
}