namespace Ama.CRDT.Models.Serialization.Converters;

using Ama.CRDT.Models.Partitioning;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// A custom <see cref="JsonConverter"/> for serializing and deserializing properties of type <see cref="IPartition"/>.
/// This converter handles polymorphism by embedding a type discriminator ('$type') in the JSON.
/// It uses the same type registry as <see cref="PolymorphicObjectJsonConverter"/>.
/// </summary>
public sealed class PolymorphicPartitionJsonConverter : JsonConverter<IPartition>
{
    public static PolymorphicPartitionJsonConverter Instance { get; } = new();

    private const string TypeDiscriminator = "$type";
    private const string ValueProperty = "value";

    public override IPartition? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
        if (!PolymorphicObjectJsonConverter.TypeMap.TryGetValue(typeDiscriminator, out var targetType))
        {
            targetType = Type.GetType(typeDiscriminator);
            if (targetType is null)
            {
                throw new NotSupportedException($"Type with discriminator '{typeDiscriminator}' is not registered or supported.");
            }
        }

        var tempOptions = new JsonSerializerOptions(options);
        if (tempOptions.Converters.FirstOrDefault(c => c is PolymorphicPartitionJsonConverter) is { } self)
        {
            tempOptions.Converters.Remove(self);
        }

        object? value;
        if (jsonObject.TryGetPropertyValue(ValueProperty, out var valueNode))
        {
            value = valueNode?.Deserialize(targetType, tempOptions);
        }
        else
        {
            jsonObject.Remove(TypeDiscriminator);
            value = jsonObject.Deserialize(targetType, tempOptions);
        }
        
        return (IPartition?)value;
    }

    public override void Write(Utf8JsonWriter writer, IPartition value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var type = value.GetType();
        if (!PolymorphicObjectJsonConverter.DiscriminatorMap.TryGetValue(type, out var typeDiscriminator))
        {
            typeDiscriminator = type.AssemblyQualifiedName!;
        }

        var tempOptions = new JsonSerializerOptions(options);
        if (tempOptions.Converters.FirstOrDefault(c => c is PolymorphicPartitionJsonConverter) is { } self)
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