namespace Ama.CRDT.Services;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines a factory for creating instances of <see cref="ICrdtPatcher"/> for a specific replica.
/// This is the primary mechanism for creating replica-specific services in a multi-writer environment,
/// ensuring that all generated operations are correctly attributed to their source.
/// </summary>
public interface ICrdtPatcherFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="ICrdtPatcher"/> configured for a specific replica ID.
    /// The returned patcher will attach this ID to all operations it generates. This is crucial for
    /// causality tracking and conflict resolution in a distributed system.
    /// </summary>
    /// <param name="replicaId">The unique identifier for the replica (e.g., a user session ID, a server node name).</param>
    /// <returns>A new <see cref="ICrdtPatcher"/> instance scoped to the given replica ID.</returns>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="replicaId"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // In your DI setup (e.g., Startup.cs)
    /// // services.AddCrdt();
    /// 
    /// // In your service/controller
    /// public class DocumentService
    /// {
    ///     private readonly ICrdtPatcherFactory patcherFactory;
    /// 
    ///     public DocumentService(ICrdtPatcherFactory patcherFactory)
    ///     {
    ///         this.patcherFactory = patcherFactory;
    ///     }
    /// 
    ///     public CrdtPatch UpdateDocument(string sessionId, CrdtDocument<MyDoc> from, CrdtDocument<MyDoc> to)
    ///     {
    ///         // Create a patcher specifically for this user's session
    ///         var patcher = patcherFactory.Create(sessionId);
    ///         return patcher.GeneratePatch(from, to);
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    ICrdtPatcher Create([DisallowNull] string replicaId);
}