namespace Ama.CRDT.UnitTests.Models.Serialization;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Shouldly;
using System;
using System.Text.Json;
using Xunit;

public readonly record struct CustomTimestamp(int Value) : ICrdtTimestamp
{
    public int CompareTo(ICrdtTimestamp? other) => other is CustomTimestamp ct ? Value.CompareTo(ct.Value) : 0;
}

public readonly record struct AnotherCustomTimestamp(string Value) : ICrdtTimestamp
{
    public int CompareTo(ICrdtTimestamp? other) => other is AnotherCustomTimestamp act ? string.Compare(Value, act.Value, StringComparison.Ordinal) : 0;
}

public sealed class InvalidTimestamp
{
    public int Value { get; set; }
}

public sealed class CrdtTimestampJsonConverterTests
{
    private readonly JsonSerializerOptions serializerOptions;

    public CrdtTimestampJsonConverterTests()
    {
        serializerOptions = TestOptionsHelper.GetDefaultOptions();
        CrdtTypeRegistry.Register("custom", typeof(CustomTimestamp));
    }

    [Fact]
    public void Serialize_WithEpochTimestamp_ShouldProduceCorrectJson()
    {
        ICrdtTimestamp timestamp = new EpochTimestamp(1234567890);

        var json = JsonSerializer.Serialize(timestamp, serializerOptions);

        // EpochTimestamp natively maps its ReplicaId property
        json.ShouldBe("{\"$type\":\"epoch\",\"Value\":1234567890,\"ReplicaId\":null}");
    }

    [Fact]
    public void Serialize_WithRegisteredCustomTimestamp_ShouldProduceCorrectJson()
    {
        ICrdtTimestamp timestamp = new CustomTimestamp(99);

        var json = JsonSerializer.Serialize(timestamp, serializerOptions);

        json.ShouldBe("{\"$type\":\"custom\",\"Value\":99}");
    }

    [Fact]
    public void Serialize_WithUnregisteredCustomTimestamp_ShouldThrowNotSupportedException()
    {
        ICrdtTimestamp timestamp = new AnotherCustomTimestamp("test");

        Should.Throw<NotSupportedException>(() => JsonSerializer.Serialize(timestamp, serializerOptions));
    }

    [Fact]
    public void Serialize_WithNullTimestamp_ShouldProduceNullJson()
    {
        ICrdtTimestamp? timestamp = null;

        var json = JsonSerializer.Serialize(timestamp, serializerOptions);

        json.ShouldBe("null");
    }

    [Fact]
    public void Deserialize_WithEpochTimestampJson_ShouldCreateEpochTimestamp()
    {
        var json = "{\"$type\":\"epoch\",\"Value\":1234567890,\"ReplicaId\":null}";

        var timestamp = JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions);

        timestamp.ShouldNotBeNull();
        timestamp.ShouldBeOfType<EpochTimestamp>();
        ((EpochTimestamp)timestamp).Value.ShouldBe(1234567890);
    }

    [Fact]
    public void Deserialize_WithRegisteredCustomTimestampJson_ShouldCreateCustomTimestamp()
    {
        var json = "{\"$type\":\"custom\",\"Value\":99}";

        var timestamp = JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions);

        timestamp.ShouldNotBeNull();
        timestamp.ShouldBeOfType<CustomTimestamp>();
        ((CustomTimestamp)timestamp).Value.ShouldBe(99);
    }

    [Fact]
    public void Deserialize_WithUnregisteredDiscriminator_ShouldThrowJsonException()
    {
        var json = "{\"$type\":\"unregistered\",\"Value\":123}";

        // System.Text.Json native polymorphism throws JsonException for an unknown discriminator mapping.
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions));
    }

    [Fact]
    public void Deserialize_WithMissingDiscriminator_ShouldThrowNotSupportedException()
    {
        var json = "{\"Value\":123}";

        // System.Text.Json native polymorphism throws NotSupportedException when it tries to deserialize
        // an interface (ICrdtTimestamp) directly because the $type property was not found.
        Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions));
    }

    [Fact]
    public void Deserialize_WithNullJson_ShouldProduceNullObject()
    {
        var json = "null";

        var timestamp = JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions);

        timestamp.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Register_WithInvalidDiscriminator_ShouldThrowArgumentException(string? discriminator)
    {
        Should.Throw<ArgumentException>(() => CrdtTypeRegistry.Register(discriminator!, typeof(AnotherCustomTimestamp)))
            .Message.ShouldContain("Discriminator cannot be null or whitespace.");
    }

    [Fact]
    public void SerializeAndDeserialize_CrdtOperation_WithTimestamp_ShouldPreserveType()
    {
        var operation = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            "/path",
            OperationType.Upsert,
            "value",
            new CustomTimestamp(1337),
            0
        );

        var json = JsonSerializer.Serialize(operation, serializerOptions);
        var deserializedOperation = JsonSerializer.Deserialize<CrdtOperation>(json, serializerOptions);

        json.ShouldContain("\"Timestamp\":{\"$type\":\"custom\",\"Value\":1337}");

        deserializedOperation.Timestamp.ShouldNotBeNull();
        deserializedOperation.Timestamp.ShouldBeOfType<CustomTimestamp>();
        ((CustomTimestamp)deserializedOperation.Timestamp).Value.ShouldBe(1337);
    }
}