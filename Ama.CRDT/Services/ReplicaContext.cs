namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A scoped service that holds the context for a specific CRDT replica, primarily its unique identifier and its causal state per shard.
/// This object's lifetime is tied to the IServiceScope created by the <see cref="ICrdtScopeFactory"/>.
/// </summary>
public sealed class ReplicaContext
{
    private string replicaId = string.Empty;
    private ShardVersionVectorMap shardVersionVectors = new ShardVersionVectorMap();

    /// <summary>
    /// Gets or sets the unique identifier for the replica within the current scope.
    /// This property is set by the <see cref="ICrdtScopeFactory"/> when the scope is created.
    /// </summary>
    [DisallowNull]
    [NotNull]
    public string ReplicaId
    {
        get => replicaId;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            replicaId = value;
        }
    }

    /// <summary>
    /// Gets or sets the strongly-typed map representing the causal history seen by this replica, partitioned by ShardId.
    /// This allows external systems to know what operations have been synchronized per shard and request missing patches accordingly.
    /// </summary>
    [DisallowNull]
    [NotNull]
    public ShardVersionVectorMap ShardVersionVectors
    {
        get => shardVersionVectors;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            shardVersionVectors = value;
        }
    }
}