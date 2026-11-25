namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a memory-compact document containing a data model and its associated CRDT metadata in a binary, serialized form.
/// This structure minimizes heap allocations and Gen-2 garbage collection overhead for cached or warm documents.
/// </summary>
/// <typeparam name="T">The type of the data model, which must be a class.</typeparam>
public readonly record struct PackedCrdtDocument<T> : IEquatable<PackedCrdtDocument<T>> where T : class
{
    /// <summary>
    /// Gets the data model instance.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Gets the serialized CRDT metadata bytes associated with the data.
    /// </summary>
    public ReadOnlyMemory<byte> MetadataBytes { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackedCrdtDocument{T}"/> struct.
    /// </summary>
    /// <param name="data">The data model instance.</param>
    /// <param name="metadataBytes">The serialized CRDT metadata bytes associated with the data.</param>
    [JsonConstructor]
    public PackedCrdtDocument(T? data, ReadOnlyMemory<byte> metadataBytes)
    {
        Data = data;
        MetadataBytes = metadataBytes;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackedCrdtDocument{T}"/> struct with empty metadata bytes.
    /// </summary>
    /// <param name="data">The data model instance.</param>
    public PackedCrdtDocument(T? data)
    {
        Data = data;
        MetadataBytes = ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public bool Equals(PackedCrdtDocument<T> other)
    {
        if (!EqualityComparer<T?>.Default.Equals(Data, other.Data))
        {
            return false;
        }

        return MetadataBytes.Span.SequenceEqual(other.MetadataBytes.Span);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Data);
        hash.AddBytes(MetadataBytes.Span);
        return hash.ToHashCode();
    }
}