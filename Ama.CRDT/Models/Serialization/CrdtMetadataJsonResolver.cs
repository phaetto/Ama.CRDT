namespace Ama.CRDT.Models.Serialization;
using System.Collections;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models.Serialization.Converters;

/// <summary>
/// A custom JSON type info resolver for <see cref="CrdtMetadata"/>.
/// It modifies the serialization contract to omit any collection properties that are empty.
/// This results in a more compact JSON output, which is efficient for sparse metadata objects.
/// </summary>
public sealed class CrdtMetadataJsonResolver : DefaultJsonTypeInfoResolver
{
    /// <summary>
    /// Gets a singleton instance of the resolver.
    /// </summary>
    public static CrdtMetadataJsonResolver Instance { get; } = new();

    private CrdtMetadataJsonResolver()
    {
        Modifiers.Add(ApplyMetadataModifiers);
    }

    /// <summary>
    /// A modifier that omits empty collection properties when serializing <see cref="CrdtMetadata"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The type info to modify.</param>
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
                // We check for IEnumerable to identify collection-like properties, as interface types like
                // IDictionary<,> or ISet<> don't implement the non-generic ICollection.
                // We exclude strings, which are also IEnumerable.
                else if (typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) && propertyInfo.PropertyType != typeof(string))
                {
                    // At runtime, the actual value (e.g., Dictionary<,>, HashSet<>) will implement
                    // the non-generic ICollection, so we can cast to it and check its Count.
                    // This predicate also correctly handles null values, as 'null is ICollection' is false.
                    propertyInfo.ShouldSerialize = static (obj, value) => value is ICollection collection && collection.Count > 0;
                }
            }
        }
    }
}