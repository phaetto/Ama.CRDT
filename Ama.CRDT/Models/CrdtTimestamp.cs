namespace Ama.CRDT.Models;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a universal, allocation-free logical point in time for a CRDT operation.
/// Supports Wall-Clock, Lamport, Hybrid Logical Clocks (HLC), and Epoch-based timestamps.
/// </summary>
/// <param name="Ticks">The primary time component (e.g., Unix milliseconds, .NET Ticks, or Lamport counter).</param>
/// <param name="Counter">The secondary logical counter (used by Hybrid Logical Clocks when physical time doesn't advance).</param>
/// <param name="Epoch">An optional epoch, term, or generation number for cluster-wide ordering overrides.</param>
/// <param name="ReplicaId">The identifier of the replica that generated the timestamp, used for deterministic tie-breaking during collisions.</param>
public readonly record struct CrdtTimestamp(
    long Ticks,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Counter = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Epoch = 0,
    string? ReplicaId = null) : IComparable<CrdtTimestamp>, IComparable
{
    /// <summary>
    /// Represents the earliest possible timestamp.
    /// </summary>
    public static readonly CrdtTimestamp MinValue = new(0, 0, 0, string.Empty);

    /// <inheritdoc/>
    public int CompareTo(CrdtTimestamp other)
    {
        var epochCmp = Epoch.CompareTo(other.Epoch);
        if (epochCmp != 0)
        {
            return epochCmp;
        }

        var ticksCmp = Ticks.CompareTo(other.Ticks);
        if (ticksCmp != 0)
        {
            return ticksCmp;
        }

        var counterCmp = Counter.CompareTo(other.Counter);
        if (counterCmp != 0)
        {
            return counterCmp;
        }

        return string.CompareOrdinal(ReplicaId ?? string.Empty, other.ReplicaId ?? string.Empty);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is CrdtTimestamp other)
        {
            return CompareTo(other);
        }

        throw new ArgumentException($"Object must be of type {nameof(CrdtTimestamp)}.", nameof(obj));
    }

    public static bool operator <(CrdtTimestamp left, CrdtTimestamp right) => left.CompareTo(right) < 0;

    public static bool operator <=(CrdtTimestamp left, CrdtTimestamp right) => left.CompareTo(right) <= 0;

    public static bool operator >(CrdtTimestamp left, CrdtTimestamp right) => left.CompareTo(right) > 0;

    public static bool operator >=(CrdtTimestamp left, CrdtTimestamp right) => left.CompareTo(right) >= 0;
}