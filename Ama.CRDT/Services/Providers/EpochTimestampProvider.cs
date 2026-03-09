namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;
using System.Threading;

/// <summary>
/// The implementation of <see cref="ICrdtTimestampProvider"/> that generates <see cref="EpochTimestamp"/>
/// based on the current system UTC time in Unix milliseconds and the current replica's ID.
/// It ensures timestamps are strictly monotonically increasing across local calls.
/// </summary>
public sealed class EpochTimestampProvider(ReplicaContext replicaContext) : ICrdtTimestampProvider
{
    private static long _lastTicks = -1;

    /// <inheritdoc/>
    public ICrdtTimestamp Now()
    {
        long original, newValue;
        do
        {
            original = Interlocked.Read(ref _lastTicks);
            var currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            newValue = currentMs > original ? currentMs : original + 1;
        } 
        while (Interlocked.CompareExchange(ref _lastTicks, newValue, original) != original);

        return new EpochTimestamp(newValue, replicaContext.ReplicaId);
    }

    /// <inheritdoc/>
    public ICrdtTimestamp Create(long value)
    {
        return new EpochTimestamp(value, replicaContext.ReplicaId);
    }
}