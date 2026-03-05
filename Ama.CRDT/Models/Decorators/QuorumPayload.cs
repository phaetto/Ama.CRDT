namespace Ama.CRDT.Models.Decorators;
/// <summary>
/// A data structure for the payload of a quorum-bound operation.
/// It wraps the underlying strategy's proposed value.
/// </summary>
/// <param name="ProposedValue">The inner value or operation payload being proposed.</param>
public readonly record struct QuorumPayload(object? ProposedValue);