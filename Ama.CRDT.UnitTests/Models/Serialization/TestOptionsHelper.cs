namespace Ama.CRDT.UnitTests.Models.Serialization;

using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Models.Serialization.Converters;
using Ama.CRDT.UnitTests.Services.Strategies.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Helper to construct AOT-safe JsonSerializerOptions for unit tests by combining 
/// the core library context and the test-specific context.
/// </summary>
public static class TestOptionsHelper
{
    public static JsonSerializerOptions GetDefaultOptions()
    {
        var resolver = JsonTypeInfoResolver.Combine(
            TestJsonSerializerContext.Default,
            CrdtJsonContext.Default
        ).WithAddedModifier(CrdtJsonTypeInfoResolver.ApplyCrdtModifiers);

        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        options.Converters.Add(CrdtPayloadJsonConverterFactory.Instance);
        options.Converters.Add(new ObjectKeyDictionaryJsonConverter([new InternalCrdtContext(), new SerializationTestCrdtContext()]));
        return options;
    }

    public static JsonSerializerOptions GetCompactOptions()
    {
        var resolver = JsonTypeInfoResolver.Combine(
            TestJsonSerializerContext.Default,
            CrdtJsonContext.Default
        ).WithAddedModifier(CrdtJsonTypeInfoResolver.ApplyCrdtModifiers)
         .WithAddedModifier(CrdtMetadataJsonResolver.ApplyMetadataModifiers);

        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        options.Converters.Add(CrdtPayloadJsonConverterFactory.Instance);
        options.Converters.Add(new ObjectKeyDictionaryJsonConverter([new InternalCrdtContext(), new SerializationTestCrdtContext()]));
        return options;
    }
}