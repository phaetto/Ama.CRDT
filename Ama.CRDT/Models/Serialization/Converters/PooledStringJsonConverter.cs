namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// A custom <see cref="JsonConverter{T}"/> for <see cref="string"/> that deduplicates deserialized strings
/// using an <see cref="ICrdtStringPool"/> via stack-allocated character spans.
/// </summary>
public sealed class PooledStringJsonConverter : JsonConverter<string>
{
    private const int StackAllocThreshold = 128;
    private readonly ICrdtStringPool stringPool;

    /// <summary>
    /// Gets the default shared instance using <see cref="CrdtStringPool.Shared"/>.
    /// </summary>
    public static PooledStringJsonConverter Shared { get; } = new(CrdtStringPool.Shared);

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledStringJsonConverter"/> class.
    /// </summary>
    /// <param name="stringPool">The string pool to use when reading strings.</param>
    public PooledStringJsonConverter(ICrdtStringPool stringPool)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        this.stringPool = stringPool;
    }

    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string token, but got {reader.TokenType}.");
        }

        return ReadPooledString(ref reader, stringPool);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }

    /// <inheritdoc/>
    public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException($"Expected PropertyName token, but got {reader.TokenType}.");
        }

        return ReadPooledString(ref reader, stringPool);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WritePropertyName(value);
    }

    internal static string ReadPooledString(ref Utf8JsonReader reader, ICrdtStringPool stringPool)
    {
        int maxCharCount = reader.HasValueSequence
            ? checked((int)reader.ValueSequence.Length)
            : reader.ValueSpan.Length;

        if (maxCharCount == 0)
        {
            return string.Empty;
        }

        if (maxCharCount <= StackAllocThreshold)
        {
            Span<char> buffer = stackalloc char[StackAllocThreshold];
            int charsWritten = reader.CopyString(buffer);
            return stringPool.GetOrAdd(buffer.Slice(0, charsWritten));
        }

        char[] rented = ArrayPool<char>.Shared.Rent(maxCharCount);
        try
        {
            int charsWritten = reader.CopyString(rented);
            return stringPool.GetOrAdd(rented.AsSpan(0, charsWritten));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }
}

/// <summary>
/// A custom <see cref="JsonConverter{T}"/> for <see cref="IDictionary{TKey, TValue}"/> with <see cref="string"/> keys
/// that pools dictionary keys during deserialization using an <see cref="ICrdtStringPool"/>.
/// </summary>
/// <typeparam name="TValue">The type of the dictionary values.</typeparam>
internal sealed class PooledStringKeyDictionaryJsonConverter<TValue> : JsonConverter<IDictionary<string, TValue>>
{
    private readonly ICrdtStringPool stringPool;

    public PooledStringKeyDictionaryJsonConverter(ICrdtStringPool stringPool)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        this.stringPool = stringPool;
    }

    public override IDictionary<string, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject token, but got {reader.TokenType}.");
        }

        var dict = new Dictionary<string, TValue>(StringComparer.Ordinal);
        var valueTypeInfo = options.GetTypeInfo(typeof(TValue));

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dict;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected PropertyName token, but got {reader.TokenType}.");
            }

            var key = PooledStringJsonConverter.ReadPooledString(ref reader, stringPool);

            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of JSON while reading dictionary value.");
            }

            var value = (TValue?)JsonSerializer.Deserialize(ref reader, valueTypeInfo);
            dict[key] = value!;
        }

        throw new JsonException("Unexpected end of JSON while reading dictionary.");
    }

    public override void Write(Utf8JsonWriter writer, IDictionary<string, TValue> value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        var valueTypeInfo = options.GetTypeInfo(typeof(TValue));

        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(writer, kvp.Value, valueTypeInfo);
        }

        writer.WriteEndObject();
    }
}