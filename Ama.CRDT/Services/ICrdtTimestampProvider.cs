namespace Ama.CRDT.Services;

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
    /// }
    /// 
    /// // This provider can then be registered in the DI container:
    /// // services.AddSingleton<ICrdtTimestampProvider, LogicalClockTimestampProvider>();
    /// ]]>
    /// </code>
    /// </example>
    ICrdtTimestamp Now();
}