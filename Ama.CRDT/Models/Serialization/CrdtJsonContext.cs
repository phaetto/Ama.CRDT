namespace Ama.CRDT.Models.Serialization;

using Ama.CRDT.Models.Serialization.Converters;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Provides pre-configured <see cref="JsonSerializerOptions"/> for serializing CRDT models like <see cref="CrdtPatch"/> and <see cref="CrdtMetadata"/>.
/// This class centralizes the setup of required converters and resolvers for correct and efficient serialization.
/// </summary>
/// <example>
/// <code>
/// <![CDATA[
/// // For serializing a CRDT patch
/// var patchJson = JsonSerializer.Serialize(myCrdtPatch, CrdtJsonContext.DefaultOptions);
/// var deserializedPatch = JsonSerializer.Deserialize<CrdtPatch>(patchJson, CrdtJsonContext.DefaultOptions);
///
/// // For serializing CRDT metadata with optimized, compact output
/// var metadataJson = JsonSerializer.Serialize(myCrdtMetadata, CrdtJsonContext.MetadataCompactOptions);
/// ]]>
/// </code>
/// </example>
public static class CrdtJsonContext
{
    /// <summary>
    /// Gets a pre-configured <see cref="JsonSerializerOptions"/> instance suitable for general CRDT serialization,
    /// including <see cref="CrdtPatch"/> and its operation payloads. It supports polymorphic types and custom CRDT models.
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();

    /// <summary>
    /// Gets a pre-configured <see cref="JsonSerializerOptions"/> instance specifically for serializing <see cref="CrdtMetadata"/>.
    /// It includes all default converters and a custom resolver that omits empty collections for a more compact JSON output.
    /// </summary>
    public static JsonSerializerOptions MetadataCompactOptions { get; } = CreateMetadataCompactOptions();

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = CrdtJsonTypeInfoResolver.Instance
        };
        AddCrdtConverters(options);
        return options;
    }

    private static JsonSerializerOptions CreateMetadataCompactOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(CrdtJsonTypeInfoResolver.ApplyCrdtModifiers);
        resolver.Modifiers.Add(CrdtMetadataJsonResolver.ApplyMetadataModifiers);

        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver
        };
        AddCrdtConverters(options);
        return options;
    }

    private static void AddCrdtConverters(JsonSerializerOptions options)
    {
        options.Converters.Add(PolymorphicObjectJsonConverter.Instance);
        options.Converters.Add(new CrdtTimestampJsonConverter());
        options.Converters.Add(new ObjectKeyDictionaryJsonConverter());
    }
}