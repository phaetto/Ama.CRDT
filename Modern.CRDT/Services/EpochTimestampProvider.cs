namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using System;

/// <summary>
/// The default implementation of <see cref="ICrdtTimestampProvider"/> that generates <see cref="EpochTimestamp"/>.
/// </summary>
public sealed class EpochTimestampProvider : ICrdtTimestampProvider
{
    /// <inheritdoc/>
    public ICrdtTimestamp Now()
    {
        return new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}