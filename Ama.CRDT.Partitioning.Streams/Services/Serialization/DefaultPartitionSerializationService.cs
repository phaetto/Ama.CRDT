namespace Ama.CRDT.Partitioning.Streams.Services.Serialization;

using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Partitioning.Streams.Models;
using Ama.CRDT.Partitioning.Streams.Models.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The default implementation of <see cref="IPartitionSerializationService"/>, fully Native AOT compatible.
/// </summary>
public sealed class DefaultPartitionSerializationService : IPartitionSerializationService
{
    private readonly JsonSerializerOptions serializerOptions;

    static DefaultPartitionSerializationService()
    {
        // Dynamically register our external stream-specific models to the polymorphic converter 
        // without introducing circular dependencies into the core Ama.CRDT logic.
        CrdtTypeRegistry.Register("bplus-tree-node", typeof(BPlusTreeNode));
        CrdtTypeRegistry.Register("bplus-tree-header", typeof(BTreeHeader));
        CrdtTypeRegistry.Register("data-stream-header", typeof(DataStreamHeader));
        CrdtTypeRegistry.Register("free-space-state", typeof(FreeSpaceState));
    }

    public DefaultPartitionSerializationService(
        [FromKeyedServices("Ama.CRDT")] IEnumerable<IJsonTypeInfoResolver> customResolvers)
    {
        ArgumentNullException.ThrowIfNull(customResolvers);

        var resolvers = new List<IJsonTypeInfoResolver>
        {
            StreamsJsonContext.Default,
            CrdtJsonContext.Default
        };

        foreach (var custom in customResolvers)
        {
            resolvers.Add(custom);
        }

        // Combine the core CRDT AOT context with the local Streams context cleanly
        var combinedResolver = JsonTypeInfoResolver.Combine([.. resolvers])
         .WithAddedModifier(CrdtJsonTypeInfoResolver.ApplyCrdtModifiers)
         .WithAddedModifier(CrdtMetadataJsonResolver.ApplyMetadataModifiers);

        serializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = combinedResolver
        };

        // Re-apply core converters for polymorphic objects manually for the new options instance
        serializerOptions.Converters.Add(Ama.CRDT.Models.Serialization.Converters.CrdtPayloadJsonConverterFactory.Instance);
        serializerOptions.Converters.Add(new CRDT.Models.Serialization.Converters.ObjectKeyDictionaryJsonConverter());
    }

    /// <inheritdoc/>
    public async Task WriteHeaderAsync(Stream stream, BTreeHeader header, int headerSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[headerSize];
        var typeInfo = serializerOptions.GetTypeInfo(typeof(BTreeHeader));
        
        var jsonData = JsonSerializer.SerializeToUtf8Bytes(header, typeInfo);
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

        var typeInfo = serializerOptions.GetTypeInfo(typeof(BTreeHeader));
        return (BTreeHeader)JsonSerializer.Deserialize(buffer.AsSpan(0, endOfJson), typeInfo)!;
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

        using var memStream = new MemoryStream(jsonBuffer);
        var typeInfo = serializerOptions.GetTypeInfo(typeof(BPlusTreeNode));
        return (BPlusTreeNode)(await JsonSerializer.DeserializeAsync(memStream, typeInfo, cancellationToken).ConfigureAwait(false))!;
    }

    /// <inheritdoc/>
    public async Task SerializeObjectAsync(Stream stream, object content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(content);

        var typeInfo = serializerOptions.GetTypeInfo(content.GetType());
        await JsonSerializer.SerializeAsync(stream, content, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<T?> DeserializeObjectAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var typeInfo = serializerOptions.GetTypeInfo(typeof(T));
        return (T?)await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public T? CloneObject<T>(T original)
    {
        ArgumentNullException.ThrowIfNull(original);

        var typeInfo = serializerOptions.GetTypeInfo(typeof(T));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(original, typeInfo);
        return (T?)JsonSerializer.Deserialize(bytes, typeInfo);
    }

    /// <inheritdoc/>
    public async Task<byte[]> SerializeNodeToBytesAsync(BPlusTreeNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        using var memStream = new MemoryStream();
        var typeInfo = serializerOptions.GetTypeInfo(typeof(BPlusTreeNode));
        await JsonSerializer.SerializeAsync(memStream, node, typeInfo, cancellationToken).ConfigureAwait(false);
        var jsonBytes = memStream.ToArray();

        var lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
        var result = new byte[lengthPrefix.Length + jsonBytes.Length];
        Buffer.BlockCopy(lengthPrefix, 0, result, 0, lengthPrefix.Length);
        Buffer.BlockCopy(jsonBytes, 0, result, lengthPrefix.Length, jsonBytes.Length);
        
        return result;
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