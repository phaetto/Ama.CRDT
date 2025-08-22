namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

/// <summary>
/// Defines a service for generating CRDT timestamps.
/// </summary>
public interface ICrdtTimestampProvider
{
    /// <summary>
    /// Gets the current timestamp.
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> representing the current time.</returns>
    ICrdtTimestamp Now();
}