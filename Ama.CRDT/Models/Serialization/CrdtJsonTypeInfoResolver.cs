namespace Ama.CRDT.Models.Serialization;

using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models.Serialization.Converters;

/// <summary>
/// A custom <see cref="IJsonTypeInfoResolver"/> that configures JSON serialization for CRDT types.
/// It applies the <see cref="PolymorphicObjectJsonConverter"/> to all properties of type <see cref="object"/>.
/// </summary>
public sealed class CrdtJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    /// <summary>
    /// Gets a singleton instance of the resolver.
    /// </summary>
    public static CrdtJsonTypeInfoResolver Instance { get; } = new();

    private CrdtJsonTypeInfoResolver()
    {
        Modifiers.Add(ApplyCrdtModifiers);
    }

    /// <summary>
    /// A modifier that applies the <see cref="PolymorphicObjectJsonConverter"/> to properties of type <see cref="object"/>.
    /// This ensures that values inside <see cref="CrdtOperation.Value"/> are serialized with type information,
    /// which is crucial for deserializing payloads correctly, for example within <see cref="CrdtMetadata.SeenExceptions"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The type info to modify.</param>
    public static void ApplyCrdtModifiers(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        var objectConverter = PolymorphicObjectJsonConverter.Instance;

        foreach (var property in jsonTypeInfo.Properties)
        {
            if (property.PropertyType == typeof(object))
            {
                property.CustomConverter = objectConverter;
            }
        }
    }
}