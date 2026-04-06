namespace Ama.CRDT.Partitioning.Streams.Services.Serialization;

using Ama.CRDT.Partitioning.Streams.Models;
using Ama.CRDT.Services.Serialization;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The default implementation of <see cref="IPartitionSerializationService"/>, fully Native AOT compatible,
/// now utilizing the decoupled <see cref="ICrdtSerializer"/> abstraction.
/// </summary>
public sealed class DefaultPartitionSerializationService : IPartitionSerializationService
{
    private readonly ICrdtSerializer crdtSerializer;

    public DefaultPartitionSerializationService(ICrdtSerializer crdtSerializer)
    {
        this.crdtSerializer = crdtSerializer ?? throw new ArgumentNullException(nameof(crdtSerializer));
    }

    /// <inheritdoc/>
    public async Task WriteHeaderAsync(Stream stream, BTreeHeader header, int headerSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[headerSize];
        
        var jsonData = crdtSerializer.SerializeToBytes(header);
        if (jsonData.Length > headerSize) throw new InvalidOperationException("Header size is too large.");

        jsonData.CopyTo(buffer, 0);
        await stream.WriteAsync(buffer.AsMemory(0, headerSize), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<BTreeHeader> ReadHeaderAsync(Stream stream, int headerSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[headerSize];
        await stream.ReadExactlyAsync(buffer.AsMemory(0, headerSize), cancellationToken).ConfigureAwait(false);

        int endOfJson = Array.FindLastIndex(buffer, b => b != 0) + 1;
        if (endOfJson == 0) endOfJson = headerSize;

        return crdtSerializer.DeserializeFromBytes<BTreeHeader>(buffer.AsSpan(0, endOfJson))!;
    }

    /// <inheritdoc/>
    public async Task<long> WriteNodeAsync(Stream stream, BPlusTreeNode node, long offset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(node);

        var nodeBytes = await SerializeNodeToBytesAsync(node, cancellationToken).ConfigureAwait(false);
        await WriteNodeBytesAsync(stream, nodeBytes, offset, cancellationToken).ConfigureAwait(false);
        return nodeBytes.Length;
    }

    /// <inheritdoc/>
    public async Task<BPlusTreeNode> ReadNodeAsync(Stream stream, long offset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Seek(offset, SeekOrigin.Begin);

        var lengthBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
        var length = BitConverter.ToInt32(lengthBuffer);

        var jsonBuffer = new byte[length];
        await stream.ReadExactlyAsync(jsonBuffer, cancellationToken).ConfigureAwait(false);

        return crdtSerializer.DeserializeFromBytes<BPlusTreeNode>(jsonBuffer)!;
    }

    /// <inheritdoc/>
    public async Task SerializeObjectAsync(Stream stream, object content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(content);

        await crdtSerializer.SerializeAsync(stream, content, content.GetType(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<T?> DeserializeObjectAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return await crdtSerializer.DeserializeAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public T? CloneObject<T>(T original)
    {
        ArgumentNullException.ThrowIfNull(original);
        return crdtSerializer.Clone(original);
    }

    /// <inheritdoc/>
    public Task<byte[]> SerializeNodeToBytesAsync(BPlusTreeNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        var jsonBytes = crdtSerializer.SerializeToBytes(node);
        var lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
        var result = new byte[lengthPrefix.Length + jsonBytes.Length];
        
        Buffer.BlockCopy(lengthPrefix, 0, result, 0, lengthPrefix.Length);
        Buffer.BlockCopy(jsonBytes, 0, result, lengthPrefix.Length, jsonBytes.Length);
        
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task WriteNodeBytesAsync(Stream stream, byte[] nodeBytes, long offset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(nodeBytes);

        stream.Seek(offset, SeekOrigin.Begin);
        await stream.WriteAsync(nodeBytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}