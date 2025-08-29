namespace Ama.CRDT.Models.Serialization.Converters;

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
        var operationOptions = CrdtJsonContext.DefaultOptions;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return set;
            }

            var operation = JsonSerializer.Deserialize<CrdtOperation>(ref reader, operationOptions);
            set.Add(operation);
        }

        throw new JsonException("Expected end of array.");
    }

    public override void Write(Utf8JsonWriter writer, ISet<CrdtOperation> value, JsonSerializerOptions options)
    {
        var operationOptions = CrdtJsonContext.DefaultOptions;
        writer.WriteStartArray();
        foreach (var operation in value)
        {
            JsonSerializer.Serialize(writer, operation, operationOptions);
        }
        writer.WriteEndArray();
    }
}