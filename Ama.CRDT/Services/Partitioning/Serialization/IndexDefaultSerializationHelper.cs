namespace Ama.CRDT.Services.Partitioning.Serialization;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Models.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// The default implementation of <see cref="IIndexSerializationHelper"/>, using System.Text.Json for serialization.
/// </summary>
public sealed class IndexDefaultSerializationHelper : IIndexSerializationHelper
{
    private readonly JsonSerializerOptions serializerOptions;

    public IndexDefaultSerializationHelper()
    {
        // We need to ensure that 'object' properties are handled polymorphically.
        // CrdtJsonContext.DefaultOptions uses a resolver, which may not apply to types
        // outside its source-generated graph. By adding the converter directly, we ensure
        // it's used for BPlusTreeNode's 'Keys' property.
        serializerOptions = new JsonSerializerOptions(CrdtJsonContext.DefaultOptions);
        if (!serializerOptions.Converters.Any(c => c is Models.Serialization.Converters.PolymorphicObjectJsonConverter))
        {
            serializerOptions.Converters.Add(Models.Serialization.Converters.PolymorphicObjectJsonConverter.Instance);
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
        using var memStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memStream, node, typeof(BPlusTreeNode), serializerOptions);
        var nodeData = memStream.ToArray();

        stream.Seek(offset, SeekOrigin.Begin);

        var lengthPrefix = BitConverter.GetBytes(nodeData.Length);
        await stream.WriteAsync(lengthPrefix);
        await stream.WriteAsync(nodeData);

        return lengthPrefix.Length + nodeData.Length;
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
}