namespace Modern.CRDT.Services;

/// <summary>
/// A factory for creating instances of <see cref="IJsonCrdtPatcher"/> for a specific replica.
/// </summary>
public interface IJsonCrdtPatcherFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="IJsonCrdtPatcher"/> configured for a specific replica ID.
    /// </summary>
    /// <param name="replicaId">The unique identifier for the replica.</param>
    /// <returns>A new <see cref="IJsonCrdtPatcher"/> instance.</returns>
    IJsonCrdtPatcher Create(string replicaId);
}