namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;

/// <inheritdoc/>
internal sealed class EpochTimestampProvider : ICrdtTimestampProvider
{
    /// <inheritdoc/>
    public ICrdtTimestamp Now()
    {
        return new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}