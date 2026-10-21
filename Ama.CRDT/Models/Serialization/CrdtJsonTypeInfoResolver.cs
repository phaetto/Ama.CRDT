namespace Ama.CRDT.Models.Serialization;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models;
using Ama.CRDT.Models.LargerThanMemory;
using Ama.CRDT.Models.Serialization.Converters;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// A static class that configures JSON serialization modifiers for CRDT types.
/// It applies Native System.Text.Json Polymorphism for purely object-oriented interfaces 
/// (<see cref="CrdtTimestamp"/>, <see cref="IChunk"/>, <see cref="ICrdtMetadataState"/>) making it fully AOT compatible,
/// and configures string pooling for high-repetition CRDT structural strings.
/// </summary>
public static class CrdtJsonTypeInfoResolver
{
    /// <summary>
    /// A modifier that wires up native polymorphism and default shared string pooling.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type metadata to modify.</param>
    public static void ApplyCrdtModifiers(JsonTypeInfo jsonTypeInfo)
    {
        ApplyCrdtModifiers(jsonTypeInfo, CrdtStringPool.Shared);
    }

    /// <summary>
    /// A modifier that wires up native polymorphism and string pooling using the specified <see cref="ICrdtStringPool"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type metadata to modify.</param>
    /// <param name="stringPool">The string pool to use for structural CRDT strings.</param>
    public static void ApplyCrdtModifiers(JsonTypeInfo jsonTypeInfo, ICrdtStringPool stringPool)
    {
        // Leverage Native System.Text.Json Polymorphism for pure object interfaces
        if (jsonTypeInfo.Type == typeof(IChunk) || 
            jsonTypeInfo.Type == typeof(ICrdtMetadataState))
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

        if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        var pooledConverter = ReferenceEquals(stringPool, CrdtStringPool.Shared)
            ? PooledStringJsonConverter.Shared
            : new PooledStringJsonConverter(stringPool);

        if (jsonTypeInfo.Type == typeof(CrdtOperation))
        {
            foreach (var prop in jsonTypeInfo.Properties)
            {
                if (prop.PropertyType == typeof(string) &&
                    (prop.Name.Equals(nameof(CrdtOperation.JsonPath), System.StringComparison.OrdinalIgnoreCase) ||
                     prop.Name.Equals(nameof(CrdtOperation.ReplicaId), System.StringComparison.OrdinalIgnoreCase) ||
                     prop.Name.Equals(nameof(CrdtOperation.ShardId), System.StringComparison.OrdinalIgnoreCase)))
                {
                    prop.CustomConverter = pooledConverter;
                }
            }
        }
        else if (jsonTypeInfo.Type == typeof(CausalTimestamp) ||
                 jsonTypeInfo.Type == typeof(FwwTimestamp) ||
                 jsonTypeInfo.Type == typeof(RgaIdentifier) ||
                 jsonTypeInfo.Type == typeof(LseqPathSegment))
        {
            foreach (var prop in jsonTypeInfo.Properties)
            {
                if (prop.PropertyType == typeof(string) &&
                    prop.Name.Equals("ReplicaId", System.StringComparison.OrdinalIgnoreCase))
                {
                    prop.CustomConverter = pooledConverter;
                }
            }
        }
        else if (jsonTypeInfo.Type == typeof(CrdtMetadata))
        {
            foreach (var prop in jsonTypeInfo.Properties)
            {
                if (prop.Name.Equals(nameof(CrdtMetadata.States), System.StringComparison.OrdinalIgnoreCase) &&
                    prop.PropertyType == typeof(IDictionary<string, ICrdtMetadataState>))
                {
                    prop.CustomConverter = new PooledStringKeyDictionaryJsonConverter<ICrdtMetadataState>(stringPool);
                }
                else if (prop.Name.Equals(nameof(CrdtMetadata.VersionVector), System.StringComparison.OrdinalIgnoreCase) &&
                         prop.PropertyType == typeof(IDictionary<string, long>))
                {
                    prop.CustomConverter = new PooledStringKeyDictionaryJsonConverter<long>(stringPool);
                }
            }
        }
    }
}