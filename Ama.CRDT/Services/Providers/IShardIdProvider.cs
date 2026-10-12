namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;

/// <summary>
/// Defines a service for extracting or calculating the Shard ID for a given CRDT document.
/// Shard IDs are used to partition causality tracking (Version Vectors) across different domains or groupings.
/// </summary>
public interface IShardIdProvider
{
    /// <summary>
    /// Gets the Shard ID for the specified CRDT document.
    /// </summary>
    /// <typeparam name="T">The type of the CRDT document payload.</typeparam>
    /// <param name="document">The document instance.</param>
    /// <returns>A string representing the Shard ID.</returns>
    string GetShardId<T>(CrdtDocument<T> document) where T : class;
}