namespace Ama.CRDT.Services.GarbageCollection;

using Ama.CRDT.Models;

/// <summary>
/// A heuristic-based compaction policy that considers any timestamp or version older than a specified threshold as safe to compact.
/// This is equivalent to a Time-To-Live (TTL) approach.
/// </summary>
public sealed class ThresholdCompactionPolicy : ICompactionPolicy
{
    private readonly CrdtTimestamp? thresholdTimestamp;
    private readonly long? thresholdVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicy"/> class with a wall-clock timestamp threshold.
    /// </summary>
    /// <param name="thresholdTimestamp">The maximum timestamp that is considered safe to compact. Anything less than or equal to this will be compacted.</param>
    public ThresholdCompactionPolicy(CrdtTimestamp thresholdTimestamp)
    {
        this.thresholdTimestamp = thresholdTimestamp;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicy"/> class with a logical version threshold.
    /// </summary>
    /// <param name="thresholdVersion">The maximum version that is considered safe to compact.</param>
    public ThresholdCompactionPolicy(long thresholdVersion)
    {
        this.thresholdVersion = thresholdVersion;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicy"/> class with both timestamp and version thresholds.
    /// </summary>
    /// <param name="thresholdTimestamp">The maximum timestamp that is considered safe to compact.</param>
    /// <param name="thresholdVersion">The maximum version that is considered safe to compact.</param>
    public ThresholdCompactionPolicy(CrdtTimestamp thresholdTimestamp, long thresholdVersion)
    {
        this.thresholdTimestamp = thresholdTimestamp;
        this.thresholdVersion = thresholdVersion;
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(CompactionCandidate candidate)
    {
        if (this.thresholdTimestamp.HasValue && candidate.Timestamp.HasValue)
        {
            if (candidate.Timestamp.Value.CompareTo(this.thresholdTimestamp.Value) <= 0)
            {
                return true;
            }
        }

        if (this.thresholdVersion.HasValue && candidate.Version.HasValue)
        {
            if (candidate.Version.Value <= this.thresholdVersion.Value)
            {
                return true;
            }
        }

        return false;
    }
}