namespace Ama.CRDT.Services.Serialization;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The default, Native AOT compatible implementation of <see cref="ICrdtSerializer"/> 
/// utilizing <see cref="System.Text.Json"/>.
/// </summary>
public sealed class JsonCrdtSerializer : ICrdtSerializer
{
    private readonly JsonSerializerOptions options;

    public JsonCrdtSerializer([FromKeyedServices("Ama.CRDT")] JsonSerializerOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SerializeAsync(Stream stream, object value, Type inputType, CancellationToken cancellationToken = default)
    {
        var typeInfo = options.GetTypeInfo(inputType);
        return JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public byte[] SerializeToBytes<T>(T value)
    {
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    /// <inheritdoc/>
    public byte[] SerializeToBytes(object value, Type inputType)
    {
        var typeInfo = options.GetTypeInfo(inputType);
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    /// <inheritdoc/>
    public T? DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes)
    {
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return JsonSerializer.Deserialize(bytes, typeInfo);
    }

    /// <inheritdoc/>
    public object? DeserializeFromBytes(ReadOnlySpan<byte> bytes, Type returnType)
    {
        var typeInfo = options.GetTypeInfo(returnType);
        return JsonSerializer.Deserialize(bytes, typeInfo);
    }

    /// <inheritdoc/>
    public string SerializeToString<T>(T value)
    {
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return JsonSerializer.Serialize(value, typeInfo);
    }

    /// <inheritdoc/>
    public string SerializeToString(object value, Type inputType)
    {
        var typeInfo = options.GetTypeInfo(inputType);
        return JsonSerializer.Serialize(value, typeInfo);
    }

    /// <inheritdoc/>
    public T? DeserializeFromString<T>(string data)
    {
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return JsonSerializer.Deserialize(data, typeInfo);
    }

    /// <inheritdoc/>
    public object? DeserializeFromString(string data, Type returnType)
    {
        var typeInfo = options.GetTypeInfo(returnType);
        return JsonSerializer.Deserialize(data, typeInfo);
    }

    /// <inheritdoc/>
    public T? Clone<T>(T original)
    {
        if (original is null) return default;
        
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(original, typeInfo);
        return JsonSerializer.Deserialize(bytes, typeInfo);
    }
}