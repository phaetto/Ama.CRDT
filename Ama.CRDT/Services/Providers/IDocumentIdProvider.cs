namespace Ama.CRDT.Services.Providers;

using System;

/// <summary>
/// Defines a service for extracting a unique document identifier from a document object.
/// This is used by journaling and other systems to associate operations with specific documents.
/// </summary>
public interface IDocumentIdProvider
{
    /// <summary>
    /// Extracts the document identifier from the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="obj">The document object.</param>
    /// <returns>A string representing the unique identifier of the document.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided object is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a valid identifier cannot be extracted from the object.</exception>
    string GetDocumentId<T>(T? obj);
}