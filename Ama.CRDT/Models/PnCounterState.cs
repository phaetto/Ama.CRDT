namespace Ama.CRDT.Models;

using System;

/// <summary>
/// Represents the state for a PN-Counter.
/// </summary>
/// <param name="P">The sum of positive increments.</param>
/// <param name="N">The sum of negative increments (as a positive value).</param>
public sealed record PnCounterState(decimal P, decimal N) : ICrdtMetadataState
{
    /// <inheritdoc/>
    public ICrdtMetadataState DeepClone() => this;

    /// <inheritdoc/>
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not PnCounterState otherCounter) return this;
        return new PnCounterState(Math.Max(P, otherCounter.P), Math.Max(N, otherCounter.N));
    }

    /// <inheritdoc/>
    public bool Equals(ICrdtMetadataState? other) => other is PnCounterState otherCounter && this.Equals(otherCounter);
}