using Modern.CRDT.Models;

namespace Modern.CRDT.ShowCase.Services;

/// <summary>
/// Defines the contract for a simple in-memory key-value store to simulate persistence
/// for each replica's state (document and metadata).
/// </summary>
public interface IInMemoryDatabaseService
{
    /// <summary>
    /// Retrieves the document and metadata for a given key.
    /// If no state exists for the key, it returns a new instance of the document and metadata.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="key">The unique key for the state.</param>
    /// <returns>A tuple containing the document and its metadata.</returns>
    Task<(T document, CrdtMetadata metadata)> GetStateAsync<T>(string key) where T : class, new();

    /// <summary>
    /// Saves the document and its associated metadata under a specific key.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="key">The unique key for the state.</param>
    /// <param name="document">The document to save.</param>
    /// <param name="metadata">The metadata to save.</param>
    Task SaveStateAsync<T>(string key, T document, CrdtMetadata metadata) where T : class;
}