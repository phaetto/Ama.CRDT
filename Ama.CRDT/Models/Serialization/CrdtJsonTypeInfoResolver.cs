namespace Ama.CRDT.Models.Serialization;

using System;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Models.Serialization.Converters;

/// <summary>
/// A custom <see cref="IJsonTypeInfoResolver"/> that configures JSON serialization for CRDT types.
/// It applies the required polymorphic converters for properties of type <see cref="object"/>, <see cref="IComparable"/>, and <see cref="IPartition"/>.
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
    /// A modifier that applies the polymorphic converters to interfaces and object properties.
    /// This ensures that values are serialized with type information, which is crucial for deserializing payloads correctly.
    /// </summary>
    /// <param name="jsonTypeInfo">The type info to modify.</param>
    public static void ApplyCrdtModifiers(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in jsonTypeInfo.Properties)
        {
            if (property.PropertyType == typeof(object))
            {
                property.CustomConverter = PolymorphicObjectJsonConverter.Instance;
            }
            else if (property.PropertyType == typeof(IComparable))
            {
                property.CustomConverter = PolymorphicComparableJsonConverter.Instance;
            }
            else if (property.PropertyType == typeof(IPartition))
            {
                property.CustomConverter = PolymorphicPartitionJsonConverter.Instance;
            }
        }
    }
}