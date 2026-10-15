namespace Ama.CRDT.Services.Providers;

using System;
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

    /// <summary>
    /// Gets the Shard ID for a CRDT document using its identifier and its hosted type, without requiring the materialized document object.
    /// </summary>
    /// <typeparam name="T">The type of the CRDT document payload.</typeparam>
    /// <param name="documentId">The unique identifier of the document.</param>
    /// <returns>A string representing the Shard ID.</returns>
    string GetShardId<T>(IComparable documentId) where T : class;

    /// <summary>
    /// Gets the Shard ID for a CRDT document using its identifier and its runtime <see cref="Type"/>, without requiring the materialized document object.
    /// </summary>
    /// <param name="documentId">The unique identifier of the document.</param>
    /// <param name="documentType">The runtime type of the CRDT document payload.</param>
    /// <returns>A string representing the Shard ID.</returns>
    string GetShardId(IComparable documentId, Type documentType);
}