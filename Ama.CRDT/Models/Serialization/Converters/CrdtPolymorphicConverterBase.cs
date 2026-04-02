namespace Ama.CRDT.Models.Serialization.Converters;

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// A high-performance base class for polymorphic JSON converters.
/// Eliminates the severe performance penalty and memory allocation of cloning <see cref="JsonSerializerOptions"/> mid-flight.
/// </summary>
public abstract class CrdtPolymorphicConverterBase<T> : JsonConverter<T>
{
    private const string TypeDiscriminator = "$type";
    private const string ValueProperty = "value";

    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonObject = JsonNode.Parse(ref reader)?.AsObject();
        if (jsonObject is null)
        {
            return default;
        }

        if (!jsonObject.TryGetPropertyValue(TypeDiscriminator, out var typeNode) || typeNode is null)
        {
            throw new JsonException($"Missing '{TypeDiscriminator}' discriminator property for {typeof(T).Name} deserialization.");
        }

        var typeDiscriminatorValue = typeNode.GetValue<string>()!;

        if (!CrdtTypeRegistry.TryGetType(typeDiscriminatorValue, out var targetType))
        {
            targetType = Type.GetType(typeDiscriminatorValue);
            if (targetType is null)
            {
                throw new NotSupportedException($"Type with discriminator '{typeDiscriminatorValue}' is not registered or supported.");
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

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var type = value.GetType();
        var typeDiscriminatorValue = GetDiscriminatorOrThrow(type);

        // Calling SerializeToNode with the concrete runtime type avoids infinite recursion because
        // this converter is tied to the abstract type T (like object or ICrdtTimestamp), not the concrete type.
        // This removes the need for expensive options cloning.
        var node = JsonSerializer.SerializeToNode(value, type, options);

        if (node is JsonObject jsonObject)
        {
            jsonObject.Add(TypeDiscriminator, typeDiscriminatorValue);
            jsonObject.WriteTo(writer, options);
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString(TypeDiscriminator, typeDiscriminatorValue);
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

    /// <summary>
    /// Gets the discriminator string for the specified type. Can be overridden to provide strict exception semantics.
    /// </summary>
    protected virtual string GetDiscriminatorOrThrow(Type type)
    {
        return CrdtTypeRegistry.GetDiscriminator(type) ?? type.AssemblyQualifiedName!;
    }
}