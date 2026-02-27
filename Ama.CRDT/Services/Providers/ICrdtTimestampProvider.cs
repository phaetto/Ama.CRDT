namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;

/// <summary>
/// Defines a service for generating CRDT timestamps representing wall-clock logical time.
/// This abstraction allows for custom timestamping mechanisms to be integrated primarily for LWW conflict resolution.
/// Implementations must be thread-safe if used in a concurrent environment.
/// </summary>
public interface ICrdtTimestampProvider
{
    /// <summary>
    /// Gets the current timestamp. Each call should ideally return a monotonically increasing value
    /// within the context of a single replica for wall-clock conflict resolution.
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> representing the current logical time.</returns>
    ICrdtTimestamp Now();

    /// <summary>
    /// Creates a timestamp from a raw long value. This is primarily intended for testing or serialization/deserialization scenarios
    /// where a specific timestamp needs to be reconstructed.
    /// </summary>
    /// <param name="value">The underlying value of the timestamp (e.g., ticks, milliseconds, a sequential number).</param>
    /// <returns>A new <see cref="ICrdtTimestamp"/> instance corresponding to the given value.</returns>
    ICrdtTimestamp Create(long value);
}