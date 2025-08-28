namespace Ama.CRDT.Services.Providers;

using System;
using System.Collections.Generic;
using System.Threading;
using Ama.CRDT.Models;

/// <summary>
/// A timestamp provider that generates sequential, predictable timestamps using <see cref="SequentialTimestamp"/>.
/// This is primarily intended for testing scenarios where deterministic timestamps are required.
/// This implementation is thread-safe.
/// </summary>
public sealed class SequentialTimestampProvider : ICrdtTimestampProvider
{
    private long counter = 0;

    public bool IsContinuous => true;

    /// <summary>
    /// Gets the initial timestamp value, which is 0.
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> with value 0.</returns>
    public ICrdtTimestamp Init()
    {
        return SequentialTimestamp.MinValue;
    }

    /// <summary>
    /// Generates a sequence of timestamps between two given timestamps. The boundaries are exclusive.
    /// </summary>
    /// <param name="start">The starting timestamp.</param>
    /// <param name="end">The ending timestamp.</param>
    /// <returns>An enumerable of timestamps between start and end.</returns>
    /// <exception cref="ArgumentException">Thrown if the timestamps are not of type <see cref="SequentialTimestamp"/>.</exception>
    public IEnumerable<ICrdtTimestamp> IterateBetween(ICrdtTimestamp start, ICrdtTimestamp end)
    {
        if (start is not SequentialTimestamp startTime || end is not SequentialTimestamp endTime)
        {
            throw new ArgumentException("SequentialTimestampProvider can only iterate between SequentialTimestamp instances.");
        }

        var startValue = startTime.Value;
        var endValue = endTime.Value;

        if (startValue >= endValue - 1)
        {
            yield break;
        }

        for (var i = startValue + 1; i < endValue; i++)
        {
            yield return new SequentialTimestamp(i);
        }
    }

    /// <summary>
    /// Gets the next sequential timestamp.
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> with a monotonically increasing value.</returns>
    public ICrdtTimestamp Now()
    {
        var newTimestamp = Interlocked.Increment(ref counter);
        return new SequentialTimestamp(newTimestamp);
    }

    /// <inheritdoc/>
    public ICrdtTimestamp Create(long value)
    {
        return new SequentialTimestamp(value);
    }
}