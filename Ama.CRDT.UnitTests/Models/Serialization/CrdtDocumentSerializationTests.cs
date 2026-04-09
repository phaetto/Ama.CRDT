namespace Ama.CRDT.UnitTests.Models.Serialization;

using Ama.CRDT.Models;
using Shouldly;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit;

internal sealed record DocumentSerializationTestModel(string Name, int Value);

public sealed class CrdtDocumentSerializationTests
{
    [Fact]
    public void ShouldSerializeAndDeserialize_WithPopulatedDataAndMetadata()
    {
        // Arrange
        var data = new DocumentSerializationTestModel("Test", 42);
        var metadata = CreatePopulatedMetadata();
        var document = new CrdtDocument<DocumentSerializationTestModel>(data, metadata);

        // Act
        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<CrdtDocument<DocumentSerializationTestModel>>)options.GetTypeInfo(typeof(CrdtDocument<DocumentSerializationTestModel>));
        
        var json = JsonSerializer.Serialize(document, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        // Assert
        deserialized.Data.ShouldNotBeNull();
        deserialized.Data.Name.ShouldBe("Test");
        deserialized.Data.Value.ShouldBe(42);

        deserialized.Metadata.ShouldNotBeNull();
        deserialized.Metadata.VersionVector.ShouldContainKeyAndValue("replica1", 100L);
        deserialized.ShouldBe(document);
    }

    [Fact]
    public void ShouldSerializeAndDeserialize_WithNullData()
    {
        // Arrange
        var metadata = CreatePopulatedMetadata();
        var document = new CrdtDocument<DocumentSerializationTestModel>(null, metadata);

        // Act
        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<CrdtDocument<DocumentSerializationTestModel>>)options.GetTypeInfo(typeof(CrdtDocument<DocumentSerializationTestModel>));
        
        var json = JsonSerializer.Serialize(document, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        // Assert
        deserialized.Data.ShouldBeNull();
        deserialized.Metadata.ShouldNotBeNull();
        deserialized.Metadata.VersionVector.ShouldContainKeyAndValue("replica1", 100L);
        deserialized.ShouldBe(document);
    }

    [Fact]
    public void ShouldSerializeAndDeserialize_WithNullMetadata()
    {
        // Arrange
        var data = new DocumentSerializationTestModel("Test", 42);
        var document = new CrdtDocument<DocumentSerializationTestModel>(data, null);

        // Act
        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<CrdtDocument<DocumentSerializationTestModel>>)options.GetTypeInfo(typeof(CrdtDocument<DocumentSerializationTestModel>));
        
        var json = JsonSerializer.Serialize(document, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        // Assert
        deserialized.Data.ShouldNotBeNull();
        deserialized.Data.Name.ShouldBe("Test");
        deserialized.Data.Value.ShouldBe(42);
        deserialized.Metadata.ShouldBeNull();
        deserialized.ShouldBe(document);
    }

    [Fact]
    public void ShouldSerializeAndDeserialize_UsingCompactOptions()
    {
        // Arrange
        var data = new DocumentSerializationTestModel("TestCompact", 99);
        var metadata = CreatePopulatedMetadata();
        var document = new CrdtDocument<DocumentSerializationTestModel>(data, metadata);

        // Act
        var options = TestOptionsHelper.GetCompactOptions();
        var typeInfo = (JsonTypeInfo<CrdtDocument<DocumentSerializationTestModel>>)options.GetTypeInfo(typeof(CrdtDocument<DocumentSerializationTestModel>));
        
        var json = JsonSerializer.Serialize(document, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        // Assert
        deserialized.Data.ShouldNotBeNull();
        deserialized.Data.ShouldBe(data);
        deserialized.Metadata.ShouldNotBeNull();
        deserialized.Metadata.VersionVector.ShouldContainKeyAndValue("replica1", 100L);
        deserialized.Metadata.States.ShouldBeEmpty(); // Asserting empty collections are instantiated correctly

        // Should omit empty collections in JSON representation of metadata
        json.ShouldNotContain("\"States\"");
        
        deserialized.ShouldBe(document);
    }

    private static CrdtMetadata CreatePopulatedMetadata()
    {
        var metadata = new CrdtMetadata();
        metadata.VersionVector.Add("replica1", 100L);
        return metadata;
    }
}