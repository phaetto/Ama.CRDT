namespace Ama.CRDT.Models.Serialization;

using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models.Serialization.Converters;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// A static class providing a modifier that omits empty collection properties
/// when serializing <see cref="CrdtMetadata"/>, resulting in a more compact JSON output.
/// </summary>
public static class CrdtMetadataJsonResolver
{
    /// <summary>
    /// A modifier that omits empty collection properties when serializing <see cref="CrdtMetadata"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type metadata to modify.</param>
    public static void ApplyMetadataModifiers(JsonTypeInfo jsonTypeInfo)
    {
        ApplyMetadataModifiers(jsonTypeInfo, CrdtStringPool.Shared);
    }

    /// <summary>
    /// A modifier that omits empty collection properties when serializing <see cref="CrdtMetadata"/>
    /// and pools dictionary keys using the specified <see cref="ICrdtStringPool"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type metadata to modify.</param>
    /// <param name="stringPool">The string pool to use for dictionary keys.</param>
    public static void ApplyMetadataModifiers(JsonTypeInfo jsonTypeInfo, ICrdtStringPool stringPool)
    {
        if (jsonTypeInfo.Type == typeof(CrdtMetadata))
        {
            foreach (JsonPropertyInfo propertyInfo in jsonTypeInfo.Properties)
            {
                if (propertyInfo.Name == nameof(CrdtMetadata.SeenExceptions))
                {
                    propertyInfo.CustomConverter = SeenExceptionsJsonConverter.Instance;
                    propertyInfo.ShouldSerialize = static (obj, value) => value is ISet<CrdtOperation> collection && collection.Count > 0;
                }
                else if (propertyInfo.Name == nameof(CrdtMetadata.States) && propertyInfo.PropertyType == typeof(IDictionary<string, ICrdtMetadataState>))
                {
                    propertyInfo.CustomConverter ??= new PooledStringKeyDictionaryJsonConverter<ICrdtMetadataState>(stringPool);
                    propertyInfo.ShouldSerialize = static (obj, value) => value is ICollection collection && collection.Count > 0;
                }
                else if (propertyInfo.Name == nameof(CrdtMetadata.VersionVector) && propertyInfo.PropertyType == typeof(IDictionary<string, long>))
                {
                    propertyInfo.CustomConverter ??= new PooledStringKeyDictionaryJsonConverter<long>(stringPool);
                    propertyInfo.ShouldSerialize = static (obj, value) => value is ICollection collection && collection.Count > 0;
                }
                else if (typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) && propertyInfo.PropertyType != typeof(string))
                {
                    propertyInfo.ShouldSerialize = static (obj, value) => value is ICollection collection && collection.Count > 0;
                }
            }
        }
    }
}