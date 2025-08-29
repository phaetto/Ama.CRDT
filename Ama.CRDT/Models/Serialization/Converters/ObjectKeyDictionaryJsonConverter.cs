namespace Ama.CRDT.Models.Serialization.Converters;

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

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var keyType = typeToConvert.GetGenericArguments()[0];
        var valueType = typeToConvert.GetGenericArguments()[1];

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

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException("Expected start of key-value pair array.");

                reader.Read();
                var key = JsonSerializer.Deserialize<TKey>(ref reader, options)!;

                reader.Read();
                var value = JsonSerializer.Deserialize<TValue>(ref reader, options);

                if (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected end of key-value pair array.");

                dictionary[key] = value!;
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, IDictionary<TKey, TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var (key, val) in value)
            {
                writer.WriteStartArray();
                JsonSerializer.Serialize(writer, key, options);
                JsonSerializer.Serialize(writer, val, options);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }
    }
}