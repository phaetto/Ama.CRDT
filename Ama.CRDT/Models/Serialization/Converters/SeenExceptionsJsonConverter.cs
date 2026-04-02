namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class SeenExceptionsJsonConverter : JsonConverter<ISet<CrdtOperation>>
{
    public static SeenExceptionsJsonConverter Instance { get; } = new();

    private SeenExceptionsJsonConverter() { }

    public override ISet<CrdtOperation> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array.");
        }

        var set = new HashSet<CrdtOperation>();
        var operationTypeInfo = options.GetTypeInfo(typeof(CrdtOperation));

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return set;
            }

            var operation = (CrdtOperation)JsonSerializer.Deserialize(ref reader, operationTypeInfo)!;
            set.Add(operation);
        }

        throw new JsonException("Expected end of array.");
    }

    public override void Write(Utf8JsonWriter writer, ISet<CrdtOperation> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var operationTypeInfo = options.GetTypeInfo(typeof(CrdtOperation));

        foreach (var operation in value)
        {
            JsonSerializer.Serialize(writer, operation, operationTypeInfo);
        }
        writer.WriteEndArray();
    }
}