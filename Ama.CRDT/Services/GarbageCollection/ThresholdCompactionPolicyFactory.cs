namespace Ama.CRDT.Services.GarbageCollection;

using System;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;

/// <summary>
/// A factory for dynamically generating instances of <see cref="ThresholdCompactionPolicy"/>.
/// It supports configuring thresholds based on a <see cref="TimeSpan"/> (Time-To-Live) or custom threshold providers.
/// </summary>
public sealed class ThresholdCompactionPolicyFactory : ICompactionPolicyFactory
{
    private readonly Func<ICrdtTimestamp>? thresholdTimestampProvider;
    private readonly Func<long>? thresholdVersionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicyFactory"/> class using a time-to-live (TTL) and a timestamp provider.
    /// </summary>
    /// <param name="timeToLive">The duration to keep metadata before it is considered safe to compact.</param>
    /// <param name="timestampProvider">The provider used to calculate the current time.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="timestampProvider"/> is null.</exception>
    public ThresholdCompactionPolicyFactory(TimeSpan timeToLive, ICrdtTimestampProvider timestampProvider)
    {
        ArgumentNullException.ThrowIfNull(timestampProvider);

        this.thresholdTimestampProvider = () =>
        {
            var now = timestampProvider.Now();
            if (now is EpochTimestamp epoch)
            {
                return new EpochTimestamp(epoch.Value - (long)timeToLive.TotalMilliseconds);
            }

            throw new InvalidOperationException($"TimeSpan-based TTL is only supported when {nameof(ICrdtTimestampProvider)} returns {nameof(EpochTimestamp)}.");
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicyFactory"/> class with a dynamic timestamp threshold provider.
    /// </summary>
    /// <param name="thresholdTimestampProvider">A delegate that provides the maximum timestamp considered safe to compact.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="thresholdTimestampProvider"/> is null.</exception>
    public ThresholdCompactionPolicyFactory(Func<ICrdtTimestamp> thresholdTimestampProvider)
    {
        this.thresholdTimestampProvider = thresholdTimestampProvider ?? throw new ArgumentNullException(nameof(thresholdTimestampProvider));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicyFactory"/> class with a dynamic version threshold provider.
    /// </summary>
    /// <param name="thresholdVersionProvider">A delegate that provides the maximum logical version considered safe to compact.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="thresholdVersionProvider"/> is null.</exception>
    public ThresholdCompactionPolicyFactory(Func<long> thresholdVersionProvider)
    {
        this.thresholdVersionProvider = thresholdVersionProvider ?? throw new ArgumentNullException(nameof(thresholdVersionProvider));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThresholdCompactionPolicyFactory"/> class with both dynamic timestamp and version threshold providers.
    /// </summary>
    /// <param name="thresholdTimestampProvider">A delegate that provides the maximum timestamp considered safe to compact.</param>
    /// <param name="thresholdVersionProvider">A delegate that provides the maximum logical version considered safe to compact.</param>
    /// <exception cref="ArgumentNullException">Thrown if either provider is null.</exception>
    public ThresholdCompactionPolicyFactory(Func<ICrdtTimestamp> thresholdTimestampProvider, Func<long> thresholdVersionProvider)
    {
        this.thresholdTimestampProvider = thresholdTimestampProvider ?? throw new ArgumentNullException(nameof(thresholdTimestampProvider));
        this.thresholdVersionProvider = thresholdVersionProvider ?? throw new ArgumentNullException(nameof(thresholdVersionProvider));
    }

    /// <inheritdoc/>
    public ICompactionPolicy CreatePolicy()
    {
        if (this.thresholdTimestampProvider != null && this.thresholdVersionProvider != null)
        {
            return new ThresholdCompactionPolicy(this.thresholdTimestampProvider(), this.thresholdVersionProvider());
        }

        if (this.thresholdTimestampProvider != null)
        {
            return new ThresholdCompactionPolicy(this.thresholdTimestampProvider());
        }

        if (this.thresholdVersionProvider != null)
        {
            return new ThresholdCompactionPolicy(this.thresholdVersionProvider());
        }

        throw new InvalidOperationException("No threshold provider was configured for the policy factory.");
    }
}