namespace Ama.CRDT.Services.Serialization;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a format-agnostic contract for serializing and deserializing CRDT data.
/// Implementations of this interface can support different serialization protocols 
/// (e.g., System.Text.Json, BSON, MessagePack) while remaining Native AOT compatible.
/// </summary>
public interface ICrdtSerializer
{
    /// <summary>
    /// Asynchronously serializes the specified value to a stream.
    /// </summary>
    Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously serializes an object of a dynamically determined type to a stream.
    /// </summary>
    Task SerializeAsync(Stream stream, object value, Type inputType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deserializes a value of the specified type from a stream.
    /// </summary>
    Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes the specified value to a byte array.
    /// </summary>
    byte[] SerializeToBytes<T>(T value);

    /// <summary>
    /// Serializes an object of a dynamically determined type to a byte array.
    /// </summary>
    byte[] SerializeToBytes(object value, Type inputType);

    /// <summary>
    /// Deserializes a value of the specified type from a read-only span of bytes.
    /// </summary>
    T? DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Deserializes a value of a dynamically determined type from a read-only span of bytes.
    /// </summary>
    object? DeserializeFromBytes(ReadOnlySpan<byte> bytes, Type returnType);

    /// <summary>
    /// Serializes the specified value to a string representation.
    /// </summary>
    string SerializeToString<T>(T value);

    /// <summary>
    /// Serializes an object of a dynamically determined type to a string representation.
    /// </summary>
    string SerializeToString(object value, Type inputType);

    /// <summary>
    /// Deserializes a value of the specified type from a string representation.
    /// </summary>
    T? DeserializeFromString<T>(string data);

    /// <summary>
    /// Deserializes a value of a dynamically determined type from a string representation.
    /// </summary>
    object? DeserializeFromString(string data, Type returnType);

    /// <summary>
    /// Creates a deep clone of the specified original value using the underlying serialization mechanism.
    /// </summary>
    T? Clone<T>(T original);
}