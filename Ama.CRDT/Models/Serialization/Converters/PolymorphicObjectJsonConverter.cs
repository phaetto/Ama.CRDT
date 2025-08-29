namespace Ama.CRDT.Models.Serialization.Converters;

using Ama.CRDT.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// A custom <see cref="JsonConverter"/> for serializing and deserializing properties of type <see cref="object"/>.
/// This converter handles polymorphism by embedding a type discriminator ('$type') in the JSON.
/// It supports primitives, known CRDT payload types, and other complex objects recursively.
/// </summary>
public sealed class PolymorphicObjectJsonConverter : JsonConverter<object>
{
    public static PolymorphicObjectJsonConverter Instance { get; } = new();

    private const string TypeDiscriminator = "$type";
    private const string ValueProperty = "value";

    private static readonly ConcurrentDictionary<string, Type> TypeMap = new();
    private static readonly ConcurrentDictionary<Type, string> DiscriminatorMap = new();

    static PolymorphicObjectJsonConverter()
    {
        // Register primitive types
        Register("bool", typeof(bool));
        Register("byte", typeof(byte));
        Register("sbyte", typeof(sbyte));
        Register("char", typeof(char));
        Register("decimal", typeof(decimal));
        Register("double", typeof(double));
        Register("float", typeof(float));
        Register("int", typeof(int));
        Register("uint", typeof(uint));
        Register("long", typeof(long));
        Register("ulong", typeof(ulong));
        Register("short", typeof(short));
        Register("ushort", typeof(ushort));
        Register("string", typeof(string));
        Register("guid", typeof(Guid));
        Register("datetime", typeof(DateTime));
        Register("datetimeoffset", typeof(DateTimeOffset));

        // Register known CRDT model types
        Register("avg-reg", typeof(AverageRegisterValue));
        Register("patch", typeof(CrdtPatch));
        Register("op", typeof(CrdtOperation));
        Register("edge", typeof(Edge));
        Register("ex-lock-payload", typeof(ExclusiveLockPayload));
        Register("graph-edge-payload", typeof(GraphEdgePayload));
        Register("graph-vertex-payload", typeof(GraphVertexPayload));
        Register("lseq-id", typeof(LseqIdentifier));
        Register("lseq-item", typeof(LseqItem));
        Register("lseq-segment", typeof(LseqPathSegment));
        Register("ormap-add", typeof(OrMapAddItem));
        Register("ormap-remove", typeof(OrMapRemoveItem));
        Register("orset-add", typeof(OrSetAddItem));
        Register("orset-remove", typeof(OrSetRemoveItem));
        Register("pos-id", typeof(PositionalIdentifier));
        Register("pos-item", typeof(PositionalItem));
        Register("tree-add", typeof(TreeAddNodePayload));
        Register("tree-remove", typeof(TreeRemoveNodePayload));
        Register("tree-move", typeof(TreeMoveNodePayload));
        Register("vote-payload", typeof(VotePayload));
    }

    /// <summary>
    /// Registers a type with a unique discriminator for polymorphic serialization.
    /// This is intended for internal use. The public API is `ServiceCollectionExtensions.AddCrdtSerializableType`.
    /// </summary>
    /// <param name="discriminator">A short, unique string to identify the type.</param>
    /// <param name="type">The type to register.</param>
    internal static void Register(string discriminator, Type type)
    {
        if (string.IsNullOrWhiteSpace(discriminator))
            throw new ArgumentException("Discriminator cannot be null or whitespace.", nameof(discriminator));

        TypeMap[discriminator] = type;
        DiscriminatorMap[type] = discriminator;
    }

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonObject = JsonNode.Parse(ref reader)?.AsObject();
        if (jsonObject is null)
        {
            return null;
        }

        if (!jsonObject.TryGetPropertyValue(TypeDiscriminator, out var typeNode) || typeNode is null)
        {
            throw new JsonException($"Missing '{TypeDiscriminator}' discriminator for polymorphic deserialization.");
        }

        var typeDiscriminator = typeNode.GetValue<string>()!;
        if (!TypeMap.TryGetValue(typeDiscriminator, out var targetType))
        {
            targetType = Type.GetType(typeDiscriminator);
            if (targetType is null)
            {
                throw new NotSupportedException($"Type with discriminator '{typeDiscriminator}' is not registered or supported.");
            }
        }

        var tempOptions = new JsonSerializerOptions(options);
        if (tempOptions.Converters.FirstOrDefault(c => c is PolymorphicObjectJsonConverter) is { } self)
        {
            tempOptions.Converters.Remove(self);
        }

        if (jsonObject.TryGetPropertyValue(ValueProperty, out var valueNode))
        {
            return valueNode?.Deserialize(targetType, tempOptions);
        }

        jsonObject.Remove(TypeDiscriminator);
        return jsonObject.Deserialize(targetType, tempOptions);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        if (!DiscriminatorMap.TryGetValue(type, out var typeDiscriminator))
        {
            typeDiscriminator = type.AssemblyQualifiedName!;
        }

        var tempOptions = new JsonSerializerOptions(options);
        if (tempOptions.Converters.FirstOrDefault(c => c is PolymorphicObjectJsonConverter) is { } self)
        {
            tempOptions.Converters.Remove(self);
        }

        var node = JsonSerializer.SerializeToNode(value, type, tempOptions);

        if (node is JsonObject jsonObject)
        {
            jsonObject.Add(TypeDiscriminator, typeDiscriminator);
            jsonObject.WriteTo(writer, options);
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString(TypeDiscriminator, typeDiscriminator);
            writer.WritePropertyName(ValueProperty);
            if (node is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                node.WriteTo(writer, options);
            }
            writer.WriteEndObject();
        }
    }
}