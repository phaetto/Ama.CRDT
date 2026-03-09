namespace Ama.CRDT.Services;

using Ama.CRDT.Models;

/// <summary>
/// A scoped service that holds the context for a specific CRDT replica, primarily its unique identifier.
/// This object's lifetime is tied to the IServiceScope created by the <see cref="ICrdtScopeFactory"/>.
/// </summary>
public sealed class ReplicaContext
{
    /// <summary>
    /// Gets or sets the unique identifier for the replica within the current scope.
    /// This property is set by the <see cref="ICrdtScopeFactory"/> when the scope is created.
    /// </summary>
    public string ReplicaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Dotted Version Vector (DVV) representing the global causal history seen by this replica across all documents.
    /// This allows external systems to know what operations have been synchronized globally and request missing patches.
    /// </summary>
    public DottedVersionVector GlobalVersionVector { get; set; } = new DottedVersionVector();
}