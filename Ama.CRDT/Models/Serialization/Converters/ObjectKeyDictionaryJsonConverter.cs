namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A custom <see cref="JsonConverterFactory"/> for creating converters that can handle
/// dictionaries with non-string keys (e.g., <see cref="object"/>, <see cref="int"/>).
/// It serializes the dictionary as a JSON array of [key, value] pairs without relying on reflection-based dynamic generic instantiation.
/// </summary>
public sealed class ObjectKeyDictionaryJsonConverter : JsonConverterFactory
{
    private readonly IEnumerable<CrdtContext>? crdtContexts;

    public ObjectKeyDictionaryJsonConverter(IEnumerable<CrdtContext>? crdtContexts = null)
    {
        this.crdtContexts = crdtContexts;
    }

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

        return new ObjectKeyDictionaryConverterInner(keyType, valueType, crdtContexts);
    }

    private sealed class ObjectKeyDictionaryConverterInner : JsonConverter<object>
    {
        private readonly Type keyType;
        private readonly Type valueType;
        private readonly IEnumerable<CrdtContext>? crdtContexts;

        public ObjectKeyDictionaryConverterInner(Type keyType, Type valueType, IEnumerable<CrdtContext>? crdtContexts)
        {
            this.keyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
            this.valueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            this.crdtContexts = crdtContexts;
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

            JsonTypeInfo keyTypeInfo = options.GetTypeInfo(this.keyType);
            JsonTypeInfo valueTypeInfo = options.GetTypeInfo(this.valueType);
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeToConvert);

            Func<object>? createInstance = typeInfo.CreateObject;

            if (createInstance == null)
            {
                // Fallback: If STJ cannot instantiate the type directly (e.g. interfaces), we use our new AOT core
                if (crdtContexts != null)
                {
                    foreach (var context in crdtContexts)
                    {
                        var crdtInfo = context.GetTypeInfo(typeToConvert);
                        if (crdtInfo?.CreateInstance != null)
                        {
                            createInstance = crdtInfo.CreateInstance;
                            break;
                        }

                        // If typeToConvert is an interface (e.g., IDictionary<TKey, TValue>), 
                        // search through registered types for a concrete dictionary with matching key/value types.
                        if (typeToConvert.IsInterface || typeToConvert.IsAbstract)
                        {
                            foreach (var registeredType in context.GetRegisteredTypes())
                            {
                                if (registeredType.IsDictionary && 
                                    registeredType.DictionaryKeyType == this.keyType && 
                                    registeredType.DictionaryValueType == this.valueType &&
                                    registeredType.CreateInstance != null &&
                                    typeToConvert.IsAssignableFrom(registeredType.Type))
                                {
                                    createInstance = registeredType.CreateInstance;
                                    break;
                                }
                            }

                            if (createInstance != null)
                            {
                                break;
                            }
                        }
                    }
                }

                if (createInstance == null)
                {
                    throw new NotSupportedException($"Cannot create instance of {typeToConvert}. Ensure the type is explicitly registered in the provided AOT CrdtContexts.");
                }
            }

            // AOT safety: utilizing the non-generic IDictionary allows us to add entries dynamically without knowing TKey and TValue
            IDictionary dictionary = (IDictionary)createInstance();

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

            JsonTypeInfo keyTypeInfo = options.GetTypeInfo(this.keyType);
            JsonTypeInfo valueTypeInfo = options.GetTypeInfo(this.valueType);

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