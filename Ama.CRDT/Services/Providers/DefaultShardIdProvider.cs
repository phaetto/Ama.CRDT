namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;

/// <summary>
/// The default implementation of <see cref="IShardIdProvider"/> that groups all documents 
/// into a single, global "default" shard.
/// </summary>
public sealed class DefaultShardIdProvider : IShardIdProvider
{
    /// <inheritdoc/>
    public string GetShardId<T>(CrdtDocument<T> document) where T : class
    {
        return "default";
    }
}