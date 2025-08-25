namespace Ama.CRDT.Services;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines a factory for creating instances of <see cref="ICrdtPatcher"/> for a specific replica.
/// This is the primary mechanism for creating replica-specific services in a multi-writer environment.
/// </summary>
public interface ICrdtPatcherFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="ICrdtPatcher"/> configured for a specific replica ID.
    /// The returned patcher will attach this ID to all operations it generates, ensuring changes can be
    /// attributed to their source.
    /// </summary>
    /// <param name="replicaId">The unique identifier for the replica (e.g., a user session ID, a server node name).</param>
    /// <returns>A new <see cref="ICrdtPatcher"/> instance scoped to the given replica ID.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="replicaId"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var factory = serviceProvider.GetRequiredService<ICrdtPatcherFactory>();
    /// 
    /// // Create a patcher specifically for a user's session or a server node
    /// var replicaAPatcher = factory.Create("user-session-A");
    /// var replicaBPatcher = factory.Create("server-node-B");
    /// 
    /// // Now use replicaAPatcher to generate patches for changes made by user A
    /// ]]>
    /// </code>
    /// </example>
    ICrdtPatcher Create([DisallowNull] string replicaId);
}