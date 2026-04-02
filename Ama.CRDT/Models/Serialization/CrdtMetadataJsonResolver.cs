namespace Ama.CRDT.Models.Serialization;

using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models.Serialization.Converters;

/// <summary>
/// A static class providing a modifier that omits empty collection properties
/// when serializing <see cref="CrdtMetadata"/>, resulting in a more compact JSON output.
/// </summary>
public static class CrdtMetadataJsonResolver
{
    /// <summary>
    /// A modifier that omits empty collection properties when serializing <see cref="CrdtMetadata"/>.
    /// </summary>
    public static void ApplyMetadataModifiers(JsonTypeInfo jsonTypeInfo)
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
                else if (typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) && propertyInfo.PropertyType != typeof(string))
                {
                    propertyInfo.ShouldSerialize = static (obj, value) => value is ICollection collection && collection.Count > 0;
                }
            }
        }
    }
}