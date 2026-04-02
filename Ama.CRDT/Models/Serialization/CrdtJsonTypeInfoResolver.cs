namespace Ama.CRDT.Models.Serialization;

using System;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;

/// <summary>
/// A custom <see cref="IJsonTypeInfoResolver"/> that configures JSON serialization for CRDT types.
/// It applies Native System.Text.Json Polymorphism for purely object-oriented interfaces 
/// (<see cref="ICrdtTimestamp"/>, <see cref="IPartition"/>). Weak types are handled globally.
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
    /// A modifier that wires up native polymorphism.
    /// </summary>
    public static void ApplyCrdtModifiers(JsonTypeInfo jsonTypeInfo)
    {
        // Leverage Native System.Text.Json Polymorphism for pure object interfaces
        if (jsonTypeInfo.Type == typeof(ICrdtTimestamp) || jsonTypeInfo.Type == typeof(IPartition))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
            };

            foreach (var kvp in CrdtTypeRegistry.GetAll())
            {
                if (jsonTypeInfo.Type.IsAssignableFrom(kvp.Value))
                {
                    jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(kvp.Value, kvp.Key));
                }
            }
        }
    }
}