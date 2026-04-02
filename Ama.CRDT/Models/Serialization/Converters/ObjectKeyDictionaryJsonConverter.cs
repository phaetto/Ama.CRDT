namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A custom <see cref="JsonConverterFactory"/> for creating converters that can handle
/// dictionaries with non-string keys (e.g., <see cref="object"/>, <see cref="int"/>).
/// It serializes the dictionary as a JSON array of [key, value] pairs without relying on reflection-based dynamic generic instantiation.
/// </summary>
public sealed class ObjectKeyDictionaryJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert == null)
        {
            return false;
        }

        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        Type def = typeToConvert.GetGenericTypeDefinition();
        if (def != typeof(Dictionary<,>) && def != typeof(IDictionary<,>))
        {
            return false;
        }

        return typeToConvert.GetGenericArguments()[0] != typeof(string);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == null)
        {
            throw new ArgumentNullException(nameof(typeToConvert));
        }

        Type keyType = typeToConvert.GetGenericArguments()[0];
        Type valueType = typeToConvert.GetGenericArguments()[1];

        return new ObjectKeyDictionaryConverterInner(keyType, valueType);
    }

    private sealed class ObjectKeyDictionaryConverterInner : JsonConverter<object>
    {
        private readonly Type keyType;
        private readonly Type valueType;

        public ObjectKeyDictionaryConverterInner(Type keyType, Type valueType)
        {
            this.keyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
            this.valueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        }

        public override bool CanConvert(Type typeToConvert) => true;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert == null)
            {
                throw new ArgumentNullException(nameof(typeToConvert));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected start of array for dictionary with non-string keys.");
            }

            System.Text.Json.Serialization.Metadata.JsonTypeInfo keyTypeInfo = options.GetTypeInfo(this.keyType);
            System.Text.Json.Serialization.Metadata.JsonTypeInfo valueTypeInfo = options.GetTypeInfo(this.valueType);
            System.Text.Json.Serialization.Metadata.JsonTypeInfo typeInfo = options.GetTypeInfo(typeToConvert);

            if (typeInfo.CreateObject == null)
            {
                throw new NotSupportedException($"Cannot create instance of {typeToConvert}. Ensure it is explicitly registered in the active JsonSerializerContext.");
            }

            // AOT safety: utilizing the non-generic IDictionary allows us to add entries dynamically without knowing TKey and TValue
            IDictionary dictionary = (IDictionary)typeInfo.CreateObject()!;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new JsonException("Expected start of key-value pair array.");
                }

                reader.Read();
                object? key = JsonSerializer.Deserialize(ref reader, keyTypeInfo);

                if (key == null)
                {
                    throw new JsonException("Dictionary key cannot be null.");
                }

                reader.Read();
                object? value = JsonSerializer.Deserialize(ref reader, valueTypeInfo);

                if (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    throw new JsonException("Expected end of key-value pair array.");
                }

                dictionary.Add(key, value);
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();

            System.Text.Json.Serialization.Metadata.JsonTypeInfo keyTypeInfo = options.GetTypeInfo(this.keyType);
            System.Text.Json.Serialization.Metadata.JsonTypeInfo valueTypeInfo = options.GetTypeInfo(this.valueType);

            // AOT safety: utilizing the non-generic IDictionary avoids generic reflection while iterating over the dictionary
            IDictionary dictionary = (IDictionary)value;

            foreach (DictionaryEntry kvp in dictionary)
            {
                writer.WriteStartArray();

                if (kvp.Key == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    JsonSerializer.Serialize(writer, kvp.Key, keyTypeInfo);
                }

                JsonSerializer.Serialize(writer, kvp.Value, valueTypeInfo);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }
    }
}