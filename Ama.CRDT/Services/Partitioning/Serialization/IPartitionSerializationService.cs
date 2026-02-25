namespace Ama.CRDT.Services.Partitioning.Serialization;

using Ama.CRDT.Models.Partitioning;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Defines a contract for serializing and deserializing partition data and B+ Tree index components to and from a stream.
/// </summary>
public interface IPartitionSerializationService
{
    /// <summary>
    /// Writes the B+ Tree header to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="header">The header object to serialize.</param>
    /// <param name="headerSize">The fixed size allocated for the header.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteHeaderAsync(Stream stream, BTreeHeader header, int headerSize);

    /// <summary>
    /// Reads the B+ Tree header from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="headerSize">The fixed size of the header.</param>
    /// <returns>A task that resolves to the deserialized header object.</returns>
    Task<BTreeHeader> ReadHeaderAsync(Stream stream, int headerSize);

    /// <summary>
    /// Writes a B+ Tree node to the stream at a specific offset.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="node">The node object to serialize.</param>
    /// <param name="offset">The offset in the stream where writing should begin.</param>
    /// <returns>A task that resolves to the number of bytes written.</returns>
    Task<long> WriteNodeAsync(Stream stream, BPlusTreeNode node, long offset);
    
    /// <summary>
    /// Reads a B+ Tree node from the stream at a specific offset.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="offset">The offset in the stream where reading should begin.</param>
    /// <returns>A task that resolves to the deserialized node object.</returns>
    Task<BPlusTreeNode> ReadNodeAsync(Stream stream, long offset);

    /// <summary>
    /// Serializes an object to the provided stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="content">The object to serialize.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SerializeObjectAsync(Stream stream, object content);

    /// <summary>
    /// Deserializes an object from the provided stream.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>A task that resolves to the deserialized object.</returns>
    Task<T?> DeserializeObjectAsync<T>(Stream stream);

    /// <summary>
    /// Creates a deep copy of an object using the underlying serialization mechanism.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="original">The object to clone.</param>
    /// <returns>A deep copy of the object.</returns>
    T? CloneObject<T>(T original);

    /// <summary>
    /// Serializes a node to a byte array, including its length prefix.
    /// </summary>
    /// <param name="node">The node to serialize.</param>
    /// <returns>A task that resolves to the serialized node as a byte array.</returns>
    Task<byte[]> SerializeNodeToBytesAsync(BPlusTreeNode node);

    /// <summary>
    /// Writes a pre-serialized node byte array to the specified offset.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="nodeBytes">The pre-serialized node bytes.</param>
    /// <param name="offset">The offset in the stream where writing should begin.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteNodeBytesAsync(Stream stream, byte[] nodeBytes, long offset);
}