namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A custom <see cref="JsonConverterFactory"/> for creating converters that can handle
/// dictionaries with non-string keys (e.g., <see cref="object"/>, <see cref="int"/>).
/// It serializes the dictionary as a JSON array of [key, value] pairs.
/// </summary>
public sealed class ObjectKeyDictionaryJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        if (typeToConvert.GetGenericTypeDefinition() != typeof(Dictionary<,>) &&
            typeToConvert.GetGenericTypeDefinition() != typeof(IDictionary<,>))
            return false;

        return typeToConvert.GetGenericArguments()[0] != typeof(string);
    }

    [UnconditionalSuppressMessage("AOT", "IL2055:MakeGenericType", Justification = "Reference types share generic code in AOT. Value types must be explicitly included in the active JsonSerializerContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Reference types share generic code in AOT. Value types must be explicitly included in the active JsonSerializerContext.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var keyType = typeToConvert.GetGenericArguments()[0];
        var valueType = typeToConvert.GetGenericArguments()[1];

        // This utilizes MakeGenericType but is considered safe in an AOT environment ONLY if the specific 
        // dictionary types used at runtime have been statically registered via the JsonSerializerContext.
        var converterType = typeof(ObjectKeyDictionaryConverterInner<,>).MakeGenericType(keyType, valueType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class ObjectKeyDictionaryConverterInner<TKey, TValue> : JsonConverter<IDictionary<TKey, TValue>> where TKey : notnull
    {
        public override IDictionary<TKey, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected start of array for dictionary with non-string keys.");

            var dictionary = new Dictionary<TKey, TValue>();

            // Resolve TypeInfo once upfront for AOT safety during the inner loops
            var keyTypeInfo = options.GetTypeInfo(typeof(TKey));
            var valueTypeInfo = options.GetTypeInfo(typeof(TValue));

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException("Expected start of key-value pair array.");

                reader.Read();
                var key = (TKey)JsonSerializer.Deserialize(ref reader, keyTypeInfo)!;

                reader.Read();
                var value = (TValue)JsonSerializer.Deserialize(ref reader, valueTypeInfo)!;

                if (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected end of key-value pair array.");

                dictionary[key] = value;
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, IDictionary<TKey, TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            var keyTypeInfo = options.GetTypeInfo(typeof(TKey));
            var valueTypeInfo = options.GetTypeInfo(typeof(TValue));

            foreach (var kvp in value)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, kvp.Key, keyTypeInfo);
                JsonSerializer.Serialize(writer, kvp.Value, valueTypeInfo);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }
    }
}