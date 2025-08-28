using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Shouldly;
using System.Text.Json;

namespace Ama.CRDT.UnitTests.Models.Serialization;

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
        serializerOptions = new JsonSerializerOptions
        {
            Converters = { new CrdtTimestampJsonConverter() }
        };
        
        CrdtTimestampJsonConverter.Register("custom", typeof(CustomTimestamp));
    }

    [Fact]
    public void Serialize_WithEpochTimestamp_ShouldProduceCorrectJson()
    {
        ICrdtTimestamp timestamp = new EpochTimestamp(1234567890);

        var json = JsonSerializer.Serialize(timestamp, serializerOptions);

        json.ShouldBe("{\"Value\":1234567890,\"$type\":\"epoch\"}");
    }

    [Fact]
    public void Serialize_WithSequentialTimestamp_ShouldProduceCorrectJson()
    {
        ICrdtTimestamp timestamp = new SequentialTimestamp(42);

        var json = JsonSerializer.Serialize(timestamp, serializerOptions);

        json.ShouldBe("{\"Value\":42,\"$type\":\"sequential\"}");
    }

    [Fact]
    public void Serialize_WithRegisteredCustomTimestamp_ShouldProduceCorrectJson()
    {
        ICrdtTimestamp timestamp = new CustomTimestamp(99);

        var json = JsonSerializer.Serialize(timestamp, serializerOptions);

        json.ShouldBe("{\"Value\":99,\"$type\":\"custom\"}");
    }

    [Fact]
    public void Serialize_WithUnregisteredCustomTimestamp_ShouldThrowNotSupportedException()
    {
        ICrdtTimestamp timestamp = new AnotherCustomTimestamp("test");

        Should.Throw<NotSupportedException>(() => JsonSerializer.Serialize(timestamp, serializerOptions))
            .Message.ShouldContain("is not a supported or registered ICrdtTimestamp for serialization.");
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
        var json = "{\"$type\":\"epoch\",\"Value\":1234567890}";

        var timestamp = JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions);

        timestamp.ShouldNotBeNull();
        timestamp.ShouldBeOfType<EpochTimestamp>();
        ((EpochTimestamp)timestamp).Value.ShouldBe(1234567890);
    }

    [Fact]
    public void Deserialize_WithSequentialTimestampJson_ShouldCreateSequentialTimestamp()
    {
        var json = "{\"$type\":\"sequential\",\"Value\":42}";

        var timestamp = JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions);

        timestamp.ShouldNotBeNull();
        timestamp.ShouldBeOfType<SequentialTimestamp>();
        ((SequentialTimestamp)timestamp).Value.ShouldBe(42);
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
    public void Deserialize_WithUnregisteredDiscriminator_ShouldThrowNotSupportedException()
    {
        var json = "{\"$type\":\"unregistered\",\"Value\":123}";

        Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions))
            .Message.ShouldContain("is not supported or not registered.");
    }

    [Fact]
    public void Deserialize_WithMissingDiscriminator_ShouldThrowJsonException()
    {
        var json = "{\"Value\":123}";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ICrdtTimestamp>(json, serializerOptions))
            .Message.ShouldContain("Missing '$type' discriminator");
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
        Should.Throw<ArgumentException>(() => CrdtTimestampJsonConverter.Register(discriminator!, typeof(AnotherCustomTimestamp)))
            .Message.ShouldContain("Discriminator cannot be null or whitespace.");
    }

    [Fact]
    public void Register_WithInvalidType_ShouldThrowArgumentException()
    {
        Should.Throw<ArgumentException>(() => CrdtTimestampJsonConverter.Register("invalid", typeof(InvalidTimestamp)))
            .Message.ShouldContain($"must implement {nameof(ICrdtTimestamp)}.");
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
            new CustomTimestamp(1337)
        );

        var json = JsonSerializer.Serialize(operation, serializerOptions);
        var deserializedOperation = JsonSerializer.Deserialize<CrdtOperation>(json, serializerOptions);

        json.ShouldContain("\"Timestamp\":{\"Value\":1337,\"$type\":\"custom\"}");

        deserializedOperation.Timestamp.ShouldNotBeNull();
        deserializedOperation.Timestamp.ShouldBeOfType<CustomTimestamp>();
        ((CustomTimestamp)deserializedOperation.Timestamp).Value.ShouldBe(1337);
    }
}