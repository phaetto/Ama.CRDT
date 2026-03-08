namespace Ama.CRDT.Models;

using System;

/// <summary>
/// Contains the synchronization requirements for two replicas to fully catch up with each other.
/// </summary>
public readonly record struct BidirectionalSyncRequirements : IEquatable<BidirectionalSyncRequirements>
{
    /// <summary>
    /// Gets the requirements for the first replica to catch up to the second.
    /// </summary>
    public ReplicaSyncRequirement ReplicaANeedsFromB { get; init; }

    /// <summary>
    /// Gets the requirements for the second replica to catch up to the first.
    /// </summary>
    public ReplicaSyncRequirement ReplicaBNeedsFromA { get; init; }

    /// <inheritdoc/>
    public bool Equals(BidirectionalSyncRequirements other)
    {
        return ReplicaANeedsFromB.Equals(other.ReplicaANeedsFromB) &&
               ReplicaBNeedsFromA.Equals(other.ReplicaBNeedsFromA);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(ReplicaANeedsFromB, ReplicaBNeedsFromA);
    }
}