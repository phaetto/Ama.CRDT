namespace Ama.CRDT.UnitTests.Models.Serialization;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Shouldly;
using System.Text.Json;
using Xunit;

public sealed class CrdtDocumentSerializationTests
{
    private sealed record TestModel(string Name, int Value);

    [Fact]
    public void ShouldSerializeAndDeserialize_WithPopulatedDataAndMetadata()
    {
        // Arrange
        var data = new TestModel("Test", 42);
        var metadata = CreatePopulatedMetadata();
        var document = new CrdtDocument<TestModel>(data, metadata);

        // Act
        var json = JsonSerializer.Serialize(document, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<CrdtDocument<TestModel>>(json, CrdtJsonContext.DefaultOptions);

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
        var document = new CrdtDocument<TestModel>(null, metadata);

        // Act
        var json = JsonSerializer.Serialize(document, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<CrdtDocument<TestModel>>(json, CrdtJsonContext.DefaultOptions);

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
        var data = new TestModel("Test", 42);
        var document = new CrdtDocument<TestModel>(data, null);

        // Act
        var json = JsonSerializer.Serialize(document, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<CrdtDocument<TestModel>>(json, CrdtJsonContext.DefaultOptions);

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
        var data = new TestModel("TestCompact", 99);
        var metadata = CreatePopulatedMetadata();
        var document = new CrdtDocument<TestModel>(data, metadata);

        // Act
        var json = JsonSerializer.Serialize(document, CrdtJsonContext.MetadataCompactOptions);
        var deserialized = JsonSerializer.Deserialize<CrdtDocument<TestModel>>(json, CrdtJsonContext.MetadataCompactOptions);

        // Assert
        deserialized.Data.ShouldNotBeNull();
        deserialized.Data.ShouldBe(data);
        deserialized.Metadata.ShouldNotBeNull();
        deserialized.Metadata.VersionVector.ShouldContainKeyAndValue("replica1", 100L);
        deserialized.Metadata.Lww.ShouldBeEmpty(); // Asserting empty collections are instantiated correctly

        // Should omit empty collections in JSON representation of metadata
        json.ShouldNotContain("\"Lww\"");
        
        deserialized.ShouldBe(document);
    }

    private static CrdtMetadata CreatePopulatedMetadata()
    {
        var metadata = new CrdtMetadata();
        metadata.VersionVector.Add("replica1", 100L);
        return metadata;
    }
}