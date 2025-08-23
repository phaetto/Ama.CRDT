namespace Ama.CRDT.Models;
/// <summary>
/// Represents a document containing data and its associated CRDT metadata.
/// </summary>
/// <typeparam name="T">The type of the data model.</typeparam>
public readonly record struct CrdtDocument<T> where T : class
{
    /// <summary>
    /// The data model instance.
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// The CRDT metadata associated with the data.
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
    /// Initializes a new instance of the <see cref="CrdtDocument{T}"/> struct with empty metadata.
    /// </summary>
    /// <param name="data">The data model instance.</param>
    public CrdtDocument(T? data)
    {
        Data = data;
        Metadata = new CrdtMetadata();
    }
}