namespace Ama.CRDT.Models.Serialization;

using Ama.CRDT.Models.Serialization.Converters;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Provides pre-configured <see cref="JsonSerializerOptions"/> for serializing CRDT models like <see cref="CrdtPatch"/> and <see cref="CrdtMetadata"/>.
/// </summary>
public static class CrdtJsonContext
{
    public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();

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
        // The Payload converter factory strictly handles polymorphism for 'object' and 'IComparable' globally.
        // This acts as a net to catch deep values inside collections like List<IComparable> and Dictionary<object, ...>
        options.Converters.Add(CrdtPayloadJsonConverterFactory.Instance);
        options.Converters.Add(new ObjectKeyDictionaryJsonConverter());
    }
}