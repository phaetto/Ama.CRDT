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

    /// <summary>
    /// Sets the document identifier on the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="obj">The document object.</param>
    /// <param name="id">The unique identifier to set.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided object or id is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the 'Id' property cannot be written to.</exception>
    void SetDocumentId<T>(T obj, string id);

    /// <summary>
    /// Creates a new instance of the document and sets its identifier.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="id">The unique identifier to set on the newly created document.</param>
    /// <returns>A new instance of the document with the specified identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided id is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the object cannot be instantiated or the 'Id' property cannot be written to.</exception>
    T CreateDocumentWithId<T>(string id);
}