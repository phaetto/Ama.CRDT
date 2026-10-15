namespace Ama.CRDT.Services.Providers;

using System;
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

    /// <inheritdoc/>
    public string GetShardId<T>(IComparable documentId) where T : class
    {
        ArgumentNullException.ThrowIfNull(documentId);
        return "default";
    }

    /// <inheritdoc/>
    public string GetShardId(IComparable documentId, Type documentType)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(documentType);
        return "default";
    }
}