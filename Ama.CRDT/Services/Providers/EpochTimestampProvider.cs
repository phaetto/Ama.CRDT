namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;

/// <summary>
/// The implementation of <see cref="ICrdtTimestampProvider"/> that generates <see cref="EpochTimestamp"/>
/// based on the current system UTC time in Unix milliseconds.
/// </summary>
public sealed class EpochTimestampProvider : ICrdtTimestampProvider
{
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