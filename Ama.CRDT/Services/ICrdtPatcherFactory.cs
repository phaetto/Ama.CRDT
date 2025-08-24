namespace Ama.CRDT.Services;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines a factory for creating instances of <see cref="ICrdtPatcher"/> for a specific replica.
/// </summary>
public interface ICrdtPatcherFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="ICrdtPatcher"/> configured for a specific replica ID.
    /// </summary>
    /// <param name="replicaId">The unique identifier for the replica.</param>
    /// <returns>A new <see cref="ICrdtPatcher"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="replicaId"/> is null or whitespace.</exception>
    ICrdtPatcher Create([DisallowNull] string replicaId);
}