namespace Ama.CRDT.Services.Serialization;

using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A Native AOT compatible implementation of <see cref="ICrdtSerializer"/> 
/// utilizing <see cref="System.Text.Json"/> and Brotli compression.
/// </summary>
public sealed class BrotliJsonCrdtSerializer : ICrdtSerializer
{
    private readonly JsonSerializerOptions options;

    public BrotliJsonCrdtSerializer([FromKeyedServices("Ama.CRDT")] JsonSerializerOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        var brotliStream = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true);
        await using var _ = brotliStream.ConfigureAwait(false);
        
        await JsonSerializer.SerializeAsync(brotliStream, value, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SerializeAsync(Stream stream, object value, Type inputType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(inputType);

        var typeInfo = options.GetTypeInfo(inputType);
        var brotliStream = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true);
        await using var _ = brotliStream.ConfigureAwait(false);
        
        await JsonSerializer.SerializeAsync(brotliStream, value, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        var brotliStream = new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true);
        await using var _ = brotliStream.ConfigureAwait(false);
        
        return await JsonSerializer.DeserializeAsync(brotliStream, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public byte[] SerializeToBytes<T>(T value)
    {
        using var memoryStream = new MemoryStream();
        using (var brotliStream = new BrotliStream(memoryStream, CompressionLevel.Fastest))
        {
            var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
            JsonSerializer.Serialize(brotliStream, value, typeInfo);
        }

        return memoryStream.ToArray();
    }

    /// <inheritdoc/>
    public byte[] SerializeToBytes(object value, Type inputType)
    {
        ArgumentNullException.ThrowIfNull(inputType);

        using var memoryStream = new MemoryStream();
        using (var brotliStream = new BrotliStream(memoryStream, CompressionLevel.Fastest))
        {
            var typeInfo = options.GetTypeInfo(inputType);
            JsonSerializer.Serialize(brotliStream, value, typeInfo);
        }

        return memoryStream.ToArray();
    }

    /// <inheritdoc/>
    public T? DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return default;
        }

        using var memoryStream = new MemoryStream(bytes.ToArray());
        using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return JsonSerializer.Deserialize(brotliStream, typeInfo);
    }

    /// <inheritdoc/>
    public object? DeserializeFromBytes(ReadOnlySpan<byte> bytes, Type returnType)
    {
        ArgumentNullException.ThrowIfNull(returnType);

        if (bytes.IsEmpty)
        {
            return default;
        }

        using var memoryStream = new MemoryStream(bytes.ToArray());
        using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
        var typeInfo = options.GetTypeInfo(returnType);
        return JsonSerializer.Deserialize(brotliStream, typeInfo);
    }

    /// <inheritdoc/>
    public T? Clone<T>(T original)
    {
        if (original is null)
        {
            return default;
        }

        var bytes = SerializeToBytes(original);
        return DeserializeFromBytes<T>(bytes);
    }
}