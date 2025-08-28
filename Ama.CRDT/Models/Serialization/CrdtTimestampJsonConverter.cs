using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ama.CRDT.Models.Serialization;

/// <summary>
/// A custom <see cref="JsonConverter"/> for serializing and deserializing the <see cref="ICrdtTimestamp"/> interface.
/// This converter handles polymorphism by adding a "$type" discriminator to the JSON output. It is extensible,
/// allowing custom timestamp types to be registered for serialization.
/// </summary>
public sealed class CrdtTimestampJsonConverter : JsonConverter<ICrdtTimestamp>
{
    private const string TypeDiscriminator = "$type";
    private static readonly ConcurrentDictionary<string, Type> TypeMap = new();
    private static readonly ConcurrentDictionary<Type, string> DiscriminatorMap = new();

    static CrdtTimestampJsonConverter()
    {
        Register("epoch", typeof(EpochTimestamp));
        Register("sequential", typeof(SequentialTimestamp));
    }

    /// <summary>
    /// Registers a custom <see cref="ICrdtTimestamp"/> implementation for polymorphic serialization.
    /// This method is thread-safe and can be called during application startup to extend the converter.
    /// </summary>
    /// <param name="discriminator">A unique string identifier for the type, used in JSON output.</param>
    /// <param name="type">The type that implements <see cref="ICrdtTimestamp"/>.</param>
    /// <exception cref="ArgumentException">Thrown if the discriminator is empty or the type does not implement <see cref="ICrdtTimestamp"/>.</exception>
    public static void Register(string discriminator, Type type)
    {
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            throw new ArgumentException("Discriminator cannot be null or whitespace.", nameof(discriminator));
        }

        if (!typeof(ICrdtTimestamp).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type.FullName} must implement {nameof(ICrdtTimestamp)}.", nameof(type));
        }

        TypeMap[discriminator] = type;
        DiscriminatorMap[type] = discriminator;
    }


    /// <inheritdoc />
    public override ICrdtTimestamp? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonObject = JsonNode.Parse(ref reader)?.AsObject();
        if (jsonObject is null)
        {
            return null;
        }

        if (!jsonObject.TryGetPropertyValue(TypeDiscriminator, out var typeNode) || typeNode is null)
        {
            throw new JsonException($"Missing '{TypeDiscriminator}' discriminator property for ICrdtTimestamp deserialization.");
        }

        var typeDiscriminatorValue = typeNode.GetValue<string>();
        jsonObject.Remove(TypeDiscriminator);

        if (!TypeMap.TryGetValue(typeDiscriminatorValue!, out var targetType))
        {
            throw new NotSupportedException($"ICrdtTimestamp with type '{typeDiscriminatorValue}' is not supported or not registered.");
        }

        var tempOptions = new JsonSerializerOptions(options);
        if (tempOptions.Converters.FirstOrDefault(c => c is CrdtTimestampJsonConverter) is { } converter)
        {
            tempOptions.Converters.Remove(converter);
        }

        return (ICrdtTimestamp?)jsonObject.Deserialize(targetType, tempOptions);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ICrdtTimestamp value, JsonSerializerOptions options)
    {
        var type = value.GetType();

        if (!DiscriminatorMap.TryGetValue(type, out var typeDiscriminatorValue))
        {
            throw new NotSupportedException($"Type {type.FullName} is not a supported or registered ICrdtTimestamp for serialization.");
        }

        var tempOptions = new JsonSerializerOptions(options);
        if (tempOptions.Converters.FirstOrDefault(c => c is CrdtTimestampJsonConverter) is { } converter)
        {
            tempOptions.Converters.Remove(converter);
        }

        var jsonObject = JsonSerializer.SerializeToNode(value, type, tempOptions)?.AsObject();
        if (jsonObject is null)
        {
            writer.WriteNullValue();
            return;
        }
        
        jsonObject.Add(TypeDiscriminator, typeDiscriminatorValue);

        jsonObject.WriteTo(writer);
    }
}