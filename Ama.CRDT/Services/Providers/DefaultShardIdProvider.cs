namespace Ama.CRDT.Services.Providers;

using System;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// The default implementation of <see cref="IShardIdProvider"/> that groups all documents 
/// into a single, global "default" shard.
/// </summary>
public sealed class DefaultShardIdProvider : IShardIdProvider
{
    private const string DefaultShard = "default";
    private readonly ICrdtStringPool stringPool;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShardIdProvider"/> class.
    /// </summary>
    public DefaultShardIdProvider()
        : this(CrdtStringPool.Shared)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShardIdProvider"/> class with a string pool.
    /// </summary>
    /// <param name="stringPool">The string pool to use for shard identifiers.</param>
    public DefaultShardIdProvider(ICrdtStringPool stringPool)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        this.stringPool = stringPool;
    }

    /// <inheritdoc/>
    public string GetShardId<T>(CrdtDocument<T> document) where T : class
    {
        return stringPool.GetOrAdd(DefaultShard);
    }

    /// <inheritdoc/>
    public string GetShardId<T>(IComparable documentId) where T : class
    {
        ArgumentNullException.ThrowIfNull(documentId);
        return stringPool.GetOrAdd(DefaultShard);
    }

    /// <inheritdoc/>
    public string GetShardId(IComparable documentId, Type documentType)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(documentType);
        return stringPool.GetOrAdd(DefaultShard);
    }
}