namespace Ama.CRDT.Models;

/// <summary>
/// Represents the state for a PN-Counter.
/// </summary>
/// <param name="P">The sum of positive increments.</param>
/// <param name="N">The sum of negative increments (as a positive value).</param>
public sealed record PnCounterState(decimal P, decimal N);