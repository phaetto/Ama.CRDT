namespace Ama.CRDT.Services;

using Ama.CRDT.Models;

/// <summary>
/// Defines a service for generating CRDT timestamps. This allows for custom timestamping
/// mechanisms, such as logical clocks, to be integrated.
/// </summary>
public interface ICrdtTimestampProvider
{
    /// <summary>
    /// Gets the current timestamp.
    /// </summary>
    /// <returns>An instance of <see cref="ICrdtTimestamp"/> representing the current time.</returns>
    ICrdtTimestamp Now();
}