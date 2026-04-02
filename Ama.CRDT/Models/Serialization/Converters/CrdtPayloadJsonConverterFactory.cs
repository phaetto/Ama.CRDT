namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// A unified, high-performance factory that generates polymorphic converters for weakly typed properties
/// (like <see cref="object"/> or <see cref="IComparable"/>) which might contain primitives.
/// By explicitly mapping to these types globally, it catches inner array/dictionary elements flawlessly.
/// </summary>
public sealed class CrdtPayloadJsonConverterFactory : JsonConverterFactory
{
    public static CrdtPayloadJsonConverterFactory Instance { get; } = new();

    public override bool CanConvert(Type typeToConvert) 
        => typeToConvert == typeof(object) || typeToConvert == typeof(IComparable);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(CrdtPayloadJsonConverterInner<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
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
                targetType = Type.GetType(typeDiscriminatorValue!);
                if (targetType is null)
                {
                    throw new NotSupportedException($"Type with discriminator '{typeDiscriminatorValue}' is not registered.");
                }
            }

            object? value;
            if (jsonObject.TryGetPropertyValue(ValueProperty, out var valueNode))
            {
                value = valueNode?.Deserialize(targetType, options);
            }
            else
            {
                jsonObject.Remove(TypeDiscriminator);
                value = jsonObject.Deserialize(targetType, options);
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
            var discriminator = CrdtTypeRegistry.GetDiscriminator(type) ?? type.AssemblyQualifiedName!;

            // Serializing with the explicit concrete runtime type completely bypasses this generic converter
            // preventing infinite recursion natively. No cloned options needed.
            var node = JsonSerializer.SerializeToNode(value, type, options);

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