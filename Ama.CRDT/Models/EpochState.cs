namespace Ama.CRDT.Models;

using System;

/// <summary>
/// Represents the tracking state for the Epoch Bound decorator strategy.
/// </summary>
/// <param name="Epoch">The current epoch integer generation.</param>
public sealed record EpochState(int Epoch) : ICrdtMetadataState
{
    /// <inheritdoc/>
    public ICrdtMetadataState DeepClone() => this;

    /// <inheritdoc/>
    public ICrdtMetadataState Merge(ICrdtMetadataState other)
    {
        if (other is not EpochState e) return this;
        return new EpochState(Math.Max(Epoch, e.Epoch));
    }

    /// <inheritdoc/>
    public bool Equals(ICrdtMetadataState? other) => other is EpochState e && e.Epoch == Epoch;
}