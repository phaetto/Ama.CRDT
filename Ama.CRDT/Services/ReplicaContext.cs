namespace Ama.CRDT.Services;

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
}