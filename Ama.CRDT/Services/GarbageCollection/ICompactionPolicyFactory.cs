namespace Ama.CRDT.Services.GarbageCollection;

/// <summary>
/// Defines a factory that creates a compaction policy.
/// This is used to generate policies dynamically when they are needed (e.g., during garbage collection after a patch application).
/// This is highly effective for long-running scopes where time-based thresholds must be evaluated relative to the current time.
/// </summary>
public interface ICompactionPolicyFactory
{
    /// <summary>
    /// Creates a new instance of a compaction policy representing the ruleset to apply at the time of invocation.
    /// </summary>
    /// <returns>An implementation of <see cref="ICompactionPolicy"/>.</returns>
    ICompactionPolicy CreatePolicy();
}