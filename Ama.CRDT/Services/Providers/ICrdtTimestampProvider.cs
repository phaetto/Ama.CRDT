namespace Ama.CRDT.Services.Providers;

using System.Collections.Generic;
using Ama.CRDT.Models;

/// <summary>
/// Defines a service for generating CRDT timestamps. This abstraction allows for custom timestamping
/// mechanisms, such as logical clocks (e.g., Lamport, Vector) or hybrid clocks, to be integrated.
/// Implementations must be thread-safe if used in a concurrent environment.
/// </summary>
public interface ICrdtTimestampProvider
{
    /// <summary>
    /// Gets the current timestamp. Each call should ideally return a unique and monotonically increasing value
    /// within the context of a single replica.
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> representing the current logical time.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // A custom timestamp provider using a simple counter for logical time.
    /// public sealed class LogicalClockTimestampProvider : ICrdtTimestampProvider
    /// {
    ///     private long counter = 0;
    /// 
    ///     public ICrdtTimestamp Now()
    ///     {
    ///         long newTimestamp = Interlocked.Increment(ref counter);
    ///         return new EpochTimestamp(newTimestamp); // Assuming EpochTimestamp can wrap any long
    ///     }
    ///     
    ///     // NOTE: Other members of ICrdtTimestampProvider must also be implemented.
    /// }
    /// 
    /// // This provider can then be registered in the DI container:
    /// // services.AddSingleton<ICrdtTimestampProvider, LogicalClockTimestampProvider>();
    /// ]]>
    /// </code>
    /// </example>
    ICrdtTimestamp Now();

    /// <summary>
    /// Gets the initial or neutral timestamp value, often representing the beginning of time for this provider.
    /// This is useful for initializing CRDT metadata.
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> representing the initial state.</returns>
    ICrdtTimestamp Init();

    /// <summary>
    /// Generates a sequence of timestamps between two given timestamps.
    /// This is primarily useful for dense timestamp providers that can generate identifiers between any two points,
    /// such as those used in LSEQ or fractional indexing strategies.
    /// The boundaries (<paramref name="start"/>, <paramref name="end"/>) are exclusive.
    /// </summary>
    /// <param name="start">The starting timestamp boundary (exclusive).</param>
    /// <param name="end">The ending timestamp boundary (exclusive).</param>
    /// <returns>An enumerable sequence of timestamps. Returns an empty sequence if no timestamps exist between the boundaries or if the provider does not support iteration.</returns>
    IEnumerable<ICrdtTimestamp> IterateBetween(ICrdtTimestamp start, ICrdtTimestamp end);

    /// <summary>
    /// Creates a timestamp from a raw long value. This is primarily intended for testing or serialization/deserialization scenarios
    /// where a specific timestamp needs to be reconstructed.
    /// </summary>
    /// <param name="value">The underlying value of the timestamp (e.g., ticks, milliseconds, a sequential number).</param>
    /// <returns>A new <see cref="ICrdtTimestamp"/> instance corresponding to the given value.</returns>
    ICrdtTimestamp Create(long value);

    bool IsContinuous { get; }
}