namespace Ama.CRDT.Models;

/// <summary>
/// Represents a document containing a data model and its associated CRDT metadata.
/// This structure is used to pass state and metadata together to CRDT services.
/// </summary>
/// <typeparam name="T">The type of the data model, which must be a class.</typeparam>
public readonly record struct CrdtDocument<T> where T : class
{
    /// <summary>
    /// Gets the data model instance.
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// Gets the CRDT metadata associated with the data.
    /// </summary>
    public CrdtMetadata? Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtDocument{T}"/> struct.
    /// </summary>
    /// <param name="data">The data model instance.</param>
    /// <param name="metadata">The CRDT metadata associated with the data.</param>
    public CrdtDocument(T? data, CrdtMetadata? metadata)
    {
        Data = data;
        Metadata = metadata;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtDocument{T}"/> struct with new, empty metadata.
    /// </summary>
    /// <param name="data">The data model instance.</param>
    public CrdtDocument(T? data)
    {
        Data = data;
        Metadata = new CrdtMetadata();
    }
}