namespace Ama.CRDT.Models.Serialization;

using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;

/// <summary>
/// A static class that configures JSON serialization modifiers for CRDT types.
/// It applies Native System.Text.Json Polymorphism for purely object-oriented interfaces 
/// (<see cref="ICrdtTimestamp"/>, <see cref="IPartition"/>) making it fully AOT compatible.
/// </summary>
public static class CrdtJsonTypeInfoResolver
{
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