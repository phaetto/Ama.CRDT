namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// The default implementation of <see cref="ICrdtTimestampProvider"/> that generates <see cref="EpochTimestamp"/>
/// based on the current system UTC time in Unix milliseconds.
/// </summary>
public sealed class EpochTimestampProvider : ICrdtTimestampProvider
{
    /// <summary>
    /// Gets a value indicating whether the timestamps are dense. For epoch-based timestamps, this is false.
    /// </summary>
    public bool IsContinuous => false;

    /// <summary>
    /// Gets the initial timestamp value, which is 0 (the Unix epoch).
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> representing the Unix epoch.</returns>
    public ICrdtTimestamp Init()
    {
        return EpochTimestamp.MinValue;
    }

    /// <summary>
    /// Generates a sequence of millisecond-based timestamps between two given epoch timestamps. The boundaries are exclusive.
    /// </summary>
    /// <param name="start">The starting timestamp.</param>
    /// <param name="end">The ending timestamp.</param>
    /// <returns>An enumerable of timestamps between start and end.</returns>
    /// <exception cref="ArgumentException">Thrown if the timestamps are not of type <see cref="EpochTimestamp"/>.</exception>
    public IEnumerable<ICrdtTimestamp> IterateBetween(ICrdtTimestamp start, ICrdtTimestamp end)
    {
        if (start is not EpochTimestamp startTime || end is not EpochTimestamp endTime)
        {
            throw new ArgumentException("EpochTimestampProvider can only iterate between EpochTimestamp instances.");
        }

        var startValue = startTime.Value;
        var endValue = endTime.Value;

        if (startValue >= endValue - 1)
        {
            yield break;
        }

        for (var i = startValue + 1; i < endValue; i++)
        {
            yield return new EpochTimestamp(i);
        }
    }

    /// <inheritdoc/>
    public ICrdtTimestamp Now()
    {
        return new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    /// <inheritdoc/>
    public ICrdtTimestamp Create(long value)
    {
        return new EpochTimestamp(value);
    }
}