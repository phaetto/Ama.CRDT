namespace Ama.CRDT.Services.Partitioning.Serialization;

using Ama.CRDT.Models.Partitioning;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Defines a contract for serializing and deserializing B+ Tree index components (headers and nodes) to and from a stream.
/// </summary>
public interface IIndexSerializationHelper
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
}