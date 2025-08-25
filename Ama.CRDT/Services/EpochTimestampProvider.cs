namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using System;

/// <inheritdoc/>
public sealed class EpochTimestampProvider : ICrdtTimestampProvider
{
    /// <inheritdoc/>
    public ICrdtTimestamp Now()
    {
        return new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}