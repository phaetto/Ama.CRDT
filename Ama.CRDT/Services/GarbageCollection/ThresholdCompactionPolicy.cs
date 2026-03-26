namespace Ama.CRDT.Services.GarbageCollection;

using System;
using Ama.CRDT.Models;

/// <summary>
/// A heuristic-based compaction policy that considers any timestamp or version older than a specified threshold as safe to compact.
/// This is equivalent to a Time-To-Live (TTL) approach.
/// </summary>
public sealed class ThresholdCompactionPolicy : ICompactionPolicy
{
    private readonly ICrdtTimestamp? _thresholdTimestamp;
    private readonly long? _thresholdVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicy"/> class with a wall-clock timestamp threshold.
    /// </summary>
    /// <param name="thresholdTimestamp">The maximum timestamp that is considered safe to compact. Anything less than or equal to this will be compacted.</param>
    public ThresholdCompactionPolicy(ICrdtTimestamp thresholdTimestamp)
    {
        _thresholdTimestamp = thresholdTimestamp ?? throw new ArgumentNullException(nameof(thresholdTimestamp));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicy"/> class with a logical version threshold.
    /// </summary>
    /// <param name="thresholdVersion">The maximum version that is considered safe to compact.</param>
    public ThresholdCompactionPolicy(long thresholdVersion)
    {
        _thresholdVersion = thresholdVersion;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicy"/> class with both timestamp and version thresholds.
    /// </summary>
    /// <param name="thresholdTimestamp">The maximum timestamp that is considered safe to compact.</param>
    /// <param name="thresholdVersion">The maximum version that is considered safe to compact.</param>
    public ThresholdCompactionPolicy(ICrdtTimestamp thresholdTimestamp, long thresholdVersion)
    {
        _thresholdTimestamp = thresholdTimestamp ?? throw new ArgumentNullException(nameof(thresholdTimestamp));
        _thresholdVersion = thresholdVersion;
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(ICrdtTimestamp timestamp)
    {
        if (timestamp == null || _thresholdTimestamp == null)
        {
            return false;
        }

        return timestamp.CompareTo(_thresholdTimestamp) <= 0;
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(string replicaId, long version)
    {
        if (_thresholdVersion == null)
        {
            return false;
        }

        return version <= _thresholdVersion.Value;
    }
}