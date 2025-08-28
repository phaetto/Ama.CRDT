namespace Ama.CRDT.Services;

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines a factory for creating isolated scopes for CRDT replicas.
/// Each scope contains its own set of scoped CRDT services (like <see cref="ICrdtPatcher"/> and <see cref="ICrdtApplicator"/>)
/// configured with a specific replica ID.
/// </summary>
/// <remarks>
/// This is the recommended entry point for working with CRDTs in a multi-replica environment (e.g., a web server handling multiple user sessions).
/// The created <see cref="IServiceScope"/> should be disposed of when the replica operations are complete to release the scoped services.
/// </remarks>
/// <example>
/// <code>
/// <![CDATA[
/// var factory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
/// 
/// using (var userScope = factory.CreateScope("user-session-abc"))
/// {
///     var patcher = userScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
///     // ... use the patcher, which is now configured for "user-session-abc"
/// }
/// ]]>
/// </code>
/// </example>
public interface ICrdtScopeFactory
{
    /// <summary>
    /// Creates a new <see cref="IServiceScope"/> and configures it for a specific replica ID.
    /// </summary>
    /// <param name="replicaId">The unique identifier for the replica. Cannot be null or whitespace.</param>
    /// <returns>A new <see cref="IServiceScope"/> that provides scoped CRDT services for the specified replica.</returns>
    IServiceScope CreateScope([DisallowNull] string replicaId);
}