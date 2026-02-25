namespace Ama.CRDT.Services.Partitioning.Serialization;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Models.Serialization.Converters;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// The default implementation of <see cref="IPartitionSerializationService"/>, using System.Text.Json for serialization.
/// </summary>
public sealed class DefaultPartitionSerializationService : IPartitionSerializationService
{
    private readonly JsonSerializerOptions serializerOptions;

    public DefaultPartitionSerializationService()
    {
        // We need to ensure that 'object' properties are handled polymorphically.
        // CrdtJsonContext.DefaultOptions uses a resolver, which may not apply to types
        // outside its source-generated graph. By adding the converter directly, we ensure
        // it's used for BPlusTreeNode's 'Keys' property.
        serializerOptions = new JsonSerializerOptions(CrdtJsonContext.DefaultOptions);
        if (!serializerOptions.Converters.Any(c => c is PolymorphicObjectJsonConverter))
        {
            serializerOptions.Converters.Add(PolymorphicObjectJsonConverter.Instance);
        }
    }

    /// <inheritdoc/>
    public async Task WriteHeaderAsync(Stream stream, BTreeHeader header, int headerSize)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[headerSize];
        var jsonData = JsonSerializer.SerializeToUtf8Bytes(header, serializerOptions);
        if (jsonData.Length > headerSize) throw new InvalidOperationException("Header size is too large.");

        jsonData.CopyTo(buffer, 0);
        await stream.WriteAsync(buffer.AsMemory(0, headerSize));
        await stream.FlushAsync();
    }

    /// <inheritdoc/>
    public async Task<BTreeHeader> ReadHeaderAsync(Stream stream, int headerSize)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[headerSize];
        await stream.ReadExactlyAsync(buffer.AsMemory(0, headerSize));

        int endOfJson = Array.FindLastIndex(buffer, b => b != 0) + 1;
        if (endOfJson == 0) endOfJson = headerSize;

        return JsonSerializer.Deserialize<BTreeHeader>(buffer.AsSpan(0, endOfJson), serializerOptions)!;
    }

    /// <inheritdoc/>
    public async Task<long> WriteNodeAsync(Stream stream, BPlusTreeNode node, long offset)
    {
        var nodeBytes = await SerializeNodeToBytesAsync(node);
        await WriteNodeBytesAsync(stream, nodeBytes, offset);
        return nodeBytes.Length;
    }

    /// <inheritdoc/>
    public async Task<BPlusTreeNode> ReadNodeAsync(Stream stream, long offset)
    {
        stream.Seek(offset, SeekOrigin.Begin);

        var lengthBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(lengthBuffer);
        var length = BitConverter.ToInt32(lengthBuffer);

        var jsonBuffer = new byte[length];
        await stream.ReadExactlyAsync(jsonBuffer);

        using var memStream = new MemoryStream(jsonBuffer);
        return (await JsonSerializer.DeserializeAsync<BPlusTreeNode>(memStream, serializerOptions))!;
    }

    /// <inheritdoc/>
    public async Task SerializeObjectAsync(Stream stream, object content)
    {
        await JsonSerializer.SerializeAsync(stream, content, content.GetType(), serializerOptions);
    }

    /// <inheritdoc/>
    public async Task<T?> DeserializeObjectAsync<T>(Stream stream)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, serializerOptions);
    }

    /// <inheritdoc/>
    public T? CloneObject<T>(T original)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(original, serializerOptions);
        return JsonSerializer.Deserialize<T>(bytes, serializerOptions);
    }

    /// <inheritdoc/>
    public async Task<byte[]> SerializeNodeToBytesAsync(BPlusTreeNode node)
    {
        using var memStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memStream, node, typeof(BPlusTreeNode), serializerOptions);
        var jsonBytes = memStream.ToArray();

        var lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
        var result = new byte[lengthPrefix.Length + jsonBytes.Length];
        Buffer.BlockCopy(lengthPrefix, 0, result, 0, lengthPrefix.Length);
        Buffer.BlockCopy(jsonBytes, 0, result, lengthPrefix.Length, jsonBytes.Length);
        
        return result;
    }

    /// <inheritdoc/>
    public async Task WriteNodeBytesAsync(Stream stream, byte[] nodeBytes, long offset)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        await stream.WriteAsync(nodeBytes.AsMemory());
        await stream.FlushAsync();
    }
}