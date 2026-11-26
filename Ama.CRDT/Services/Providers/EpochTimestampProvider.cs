namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;
using System.Threading;

/// <summary>
/// The implementation of <see cref="ICrdtTimestampProvider"/> that generates <see cref="CrdtTimestamp"/>
/// based on the current system UTC time in Unix milliseconds and the current replica's ID.
/// It ensures timestamps are strictly monotonically increasing across local calls.
/// </summary>
public sealed class EpochTimestampProvider : ICrdtTimestampProvider
{
    private static long lastTicks = -1;

    /// <inheritdoc/>
    public CrdtTimestamp Now(string replicaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        long original, newValue;
        do
        {
            original = Interlocked.Read(ref lastTicks);
            var currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            newValue = currentMs > original ? currentMs : original + 1;
        } 
        while (Interlocked.CompareExchange(ref lastTicks, newValue, original) != original);

        return new CrdtTimestamp(newValue, 0, 0, replicaId);
    }

    /// <inheritdoc/>
    public CrdtTimestamp Create(long value, string replicaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        return new CrdtTimestamp(value, 0, 0, replicaId);
    }
}