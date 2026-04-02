namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// A unified, AOT-friendly factory that generates polymorphic converters for weakly typed properties
/// (like <see cref="object"/> or <see cref="IComparable"/>) which might contain primitives.
/// </summary>
public sealed class CrdtPayloadJsonConverterFactory : JsonConverterFactory
{
    public static CrdtPayloadJsonConverterFactory Instance { get; } = new();

    public override bool CanConvert(Type typeToConvert) 
        => typeToConvert == typeof(object) || typeToConvert == typeof(IComparable);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // By returning explicitly typed converters instead of using MakeGenericType,
        // we ensure this factory is fully AOT compatible.
        if (typeToConvert == typeof(object)) return new CrdtPayloadJsonConverterInner<object>();
        if (typeToConvert == typeof(IComparable)) return new CrdtPayloadJsonConverterInner<IComparable>();
        
        throw new NotSupportedException($"Type '{typeToConvert}' is not supported by {nameof(CrdtPayloadJsonConverterFactory)}.");
    }

    private sealed class CrdtPayloadJsonConverterInner<T> : JsonConverter<T>
    {
        private const string TypeDiscriminator = "$type";
        private const string ValueProperty = "value";

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);
            if (node is null) return default;

            if (node is not JsonObject jsonObject)
            {
                throw new JsonException($"Expected JSON object for {typeof(T).Name} payload containing discriminator.");
            }

            if (!jsonObject.TryGetPropertyValue(TypeDiscriminator, out var typeNode) || typeNode is null)
            {
                throw new JsonException($"Missing '{TypeDiscriminator}' discriminator property.");
            }

            var typeDiscriminatorValue = typeNode.GetValue<string>();
            if (!CrdtTypeRegistry.TryGetType(typeDiscriminatorValue!, out var targetType))
            {
                throw new NotSupportedException($"Type with discriminator '{typeDiscriminatorValue}' is not registered in CrdtTypeRegistry. Explicit type registration is required for AOT compatibility.");
            }

            // Fetch TypeInfo for AOT safety rather than relying on reflection deserialization
            var typeInfo = options.GetTypeInfo(targetType);
            object? value;

            if (jsonObject.TryGetPropertyValue(ValueProperty, out var valueNode))
            {
                value = valueNode?.Deserialize(typeInfo);
            }
            else
            {
                jsonObject.Remove(TypeDiscriminator);
                value = jsonObject.Deserialize(typeInfo);
            }

            return (T?)value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            var type = value.GetType();
            var discriminator = CrdtTypeRegistry.GetDiscriminator(type);

            if (discriminator is null)
            {
                throw new NotSupportedException($"Type '{type}' is not registered in CrdtTypeRegistry. Explicit type registration is required for AOT compatibility.");
            }

            // Fetch TypeInfo for AOT safety
            var typeInfo = options.GetTypeInfo(type);
            var node = JsonSerializer.SerializeToNode(value, typeInfo);

            if (node is JsonObject jsonObject)
            {
                if (!jsonObject.ContainsKey(TypeDiscriminator))
                {
                    jsonObject.Add(TypeDiscriminator, discriminator);
                }
                jsonObject.WriteTo(writer, options);
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString(TypeDiscriminator, discriminator);
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
}