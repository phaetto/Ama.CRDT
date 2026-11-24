namespace Ama.CRDT.Models.Serialization;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Collections;
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

        ConfigureCompactCollectionFactories(jsonTypeInfo, stringPool);

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

    private static void ConfigureCompactCollectionFactories(JsonTypeInfo jsonTypeInfo, ICrdtStringPool stringPool)
    {
        if (jsonTypeInfo.Kind is not (JsonTypeInfoKind.Dictionary or JsonTypeInfoKind.Enumerable))
        {
            return;
        }

        var type = jsonTypeInfo.Type;
        if (type == typeof(IDictionary<string, ICrdtMetadataState>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, ICrdtMetadataState>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<string, long>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, long>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<string, ISet<long>>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, ISet<long>>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<string, DottedVersionVector>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, DottedVersionVector>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<string, CausalTimestamp>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, CausalTimestamp>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<string, AverageRegisterValue>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, AverageRegisterValue>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<string, PnCounterState>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, PnCounterState>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<string, decimal>))
        {
            jsonTypeInfo.CreateObject = () => new CompactDictionary<string, decimal>(StringComparer.Ordinal, stringPool);
        }
        else if (type == typeof(IDictionary<object, CausalTimestamp>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<object, CausalTimestamp>();
        }
        else if (type == typeof(IDictionary<object, CrdtTimestamp>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<object, CrdtTimestamp>();
        }
        else if (type == typeof(IDictionary<object, PnCounterState>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<object, PnCounterState>();
        }
        else if (type == typeof(IDictionary<object, ISet<Guid>>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<object, ISet<Guid>>();
        }
        else if (type == typeof(IDictionary<object, ISet<string>>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<object, ISet<string>>();
        }
        else if (type == typeof(IDictionary<object, IDictionary<Guid, CausalTimestamp>>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<object, IDictionary<Guid, CausalTimestamp>>();
        }
        else if (type == typeof(IDictionary<object, TreeNode>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<object, TreeNode>();
        }
        else if (type == typeof(IDictionary<Guid, CausalTimestamp>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactDictionary<Guid, CausalTimestamp>();
        }
        else if (type == typeof(ISet<CrdtOperation>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactSet<CrdtOperation>();
        }
        else if (type == typeof(ISet<object>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactSet<object>();
        }
        else if (type == typeof(ISet<Edge>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactSet<Edge>();
        }
        else if (type == typeof(ISet<Guid>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactSet<Guid>();
        }
        else if (type == typeof(ISet<long>))
        {
            jsonTypeInfo.CreateObject = static () => new CompactSet<long>();
        }
        else if (type == typeof(ISet<string>))
        {
            jsonTypeInfo.CreateObject = () => new CompactSet<string>(StringComparer.Ordinal, stringPool);
        }
    }
}