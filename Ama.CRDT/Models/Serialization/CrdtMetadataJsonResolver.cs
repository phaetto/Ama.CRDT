namespace Ama.CRDT.Models.Serialization;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// A custom JSON type info resolver for <see cref="CrdtMetadata"/>.
/// It modifies the serialization contract to omit any collection properties that are empty.
/// This results in a more compact JSON output, which is efficient for sparse metadata objects.
/// </summary>
public sealed class CrdtMetadataJsonResolver : DefaultJsonTypeInfoResolver
{
    /// <inheritdoc />
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

        if (jsonTypeInfo.Type == typeof(CrdtMetadata))
        {
            foreach (JsonPropertyInfo propertyInfo in jsonTypeInfo.Properties)
            {
                if (typeof(ICollection).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    propertyInfo.ShouldSerialize = static (obj, value) => value is ICollection collection && collection.Count > 0;
                }
            }
        }

        return jsonTypeInfo;
    }
}