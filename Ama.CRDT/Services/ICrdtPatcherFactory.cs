namespace Ama.CRDT.Services;
/// <summary>
/// A factory for creating instances of <see cref="ICrdtPatcher"/> for a specific replica.
/// </summary>
public interface ICrdtPatcherFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="ICrdtPatcher"/> configured for a specific replica ID.
    /// </summary>
    /// <param name="replicaId">The unique identifier for the replica.</param>
    /// <returns>A new <see cref="ICrdtPatcher"/> instance.</returns>
    ICrdtPatcher Create(string replicaId);
}