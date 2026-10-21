namespace Ama.CRDT.UnitTests.Models.Serialization;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Shouldly;
using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit;

public readonly record struct CustomPayload(int Value);

public readonly record struct UnregisteredPayload(string Value);

public sealed class CrdtTimestampJsonConverterTests
{
    private readonly JsonSerializerOptions serializerOptions;

    public CrdtTimestampJsonConverterTests()
    {
        serializerOptions = TestOptionsHelper.GetDefaultOptions();
        CrdtTypeRegistry.Register("custom-payload", typeof(CustomPayload));
    }

    [Fact]
    public void Serialize_WithBasicTimestamp_ShouldOmitDefaultProperties()
    {
        var timestamp = new CrdtTimestamp(1234567890);

        var typeInfo = (JsonTypeInfo<CrdtTimestamp>)serializerOptions.GetTypeInfo(typeof(CrdtTimestamp));
        var json = JsonSerializer.Serialize(timestamp, typeInfo);

        // Counter (0), Epoch (0), and ReplicaId (null) are omitted by WhenWritingDefault / WhenWritingNull
        json.ShouldBe("{\"Ticks\":1234567890,\"ReplicaId\":null}");
    }

    [Fact]
    public void Serialize_WithFullTimestamp_ShouldIncludeAllProperties()
    {
        var timestamp = new CrdtTimestamp(Ticks: 1234567890, Counter: 5, Epoch: 2, ReplicaId: "replica-1");

        var typeInfo = (JsonTypeInfo<CrdtTimestamp>)serializerOptions.GetTypeInfo(typeof(CrdtTimestamp));
        var json = JsonSerializer.Serialize(timestamp, typeInfo);

        json.ShouldBe("{\"Ticks\":1234567890,\"Counter\":5,\"Epoch\":2,\"ReplicaId\":\"replica-1\"}");
    }

    [Fact]
    public void Deserialize_WithBasicTimestampJson_ShouldCreateTimestampWithDefaults()
    {
        var json = "{\"Ticks\":1234567890}";
        var typeInfo = (JsonTypeInfo<CrdtTimestamp>)serializerOptions.GetTypeInfo(typeof(CrdtTimestamp));

        var timestamp = JsonSerializer.Deserialize(json, typeInfo);

        timestamp.Ticks.ShouldBe(1234567890);
        timestamp.Counter.ShouldBe(0);
        timestamp.Epoch.ShouldBe(0);
        timestamp.ReplicaId.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_WithFullTimestampJson_ShouldCreateFullTimestamp()
    {
        var json = "{\"Ticks\":1234567890,\"Counter\":5,\"Epoch\":2,\"ReplicaId\":\"replica-1\"}";
        var typeInfo = (JsonTypeInfo<CrdtTimestamp>)serializerOptions.GetTypeInfo(typeof(CrdtTimestamp));

        var timestamp = JsonSerializer.Deserialize(json, typeInfo);

        timestamp.Ticks.ShouldBe(1234567890);
        timestamp.Counter.ShouldBe(5);
        timestamp.Epoch.ShouldBe(2);
        timestamp.ReplicaId.ShouldBe("replica-1");
    }

    [Fact]
    public void SerializeAndDeserialize_CrdtOperation_WithTimestamp_ShouldPreserveTimestamp()
    {
        var timestamp = new CrdtTimestamp(Ticks: 1337, Counter: 3, Epoch: 1, ReplicaId: "replica-1");
        var operation = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            "/path",
            OperationType.Upsert,
            "value",
            timestamp,
            0
        );

        var typeInfo = (JsonTypeInfo<CrdtOperation>)serializerOptions.GetTypeInfo(typeof(CrdtOperation));
        var json = JsonSerializer.Serialize(operation, typeInfo);
        var deserializedOperation = JsonSerializer.Deserialize(json, typeInfo);

        json.ShouldContain("\"Timestamp\":{\"Ticks\":1337,\"Counter\":3,\"Epoch\":1,\"ReplicaId\":\"replica-1\"}");
        deserializedOperation.Timestamp.ShouldBe(timestamp);
    }

    [Fact]
    public void SerializeAndDeserialize_TimestampAsPolymorphicPayload_ShouldUseDiscriminator()
    {
        var timestamp = new CrdtTimestamp(Ticks: 987654321, Counter: 1, Epoch: 0, ReplicaId: "r1");
        var operation = new CrdtOperation(
            Guid.NewGuid(),
            "r1",
            "/path",
            OperationType.Upsert,
            timestamp, // Boxed as object in CrdtOperation.Value
            new CrdtTimestamp(1000),
            1
        );

        var typeInfo = (JsonTypeInfo<CrdtOperation>)serializerOptions.GetTypeInfo(typeof(CrdtOperation));
        var json = JsonSerializer.Serialize(operation, typeInfo);
        var deserializedOperation = JsonSerializer.Deserialize(json, typeInfo);

        json.ShouldContain("\"$type\":\"crdt-timestamp\"");
        deserializedOperation.Value.ShouldBeOfType<CrdtTimestamp>();
        ((CrdtTimestamp)deserializedOperation.Value!).ShouldBe(timestamp);
    }

    [Fact]
    public void SerializeAndDeserialize_RegisteredCustomPayload_ShouldProduceCorrectJson()
    {
        var operation = new CrdtOperation(
            Guid.NewGuid(),
            "r1",
            "/path",
            OperationType.Upsert,
            new CustomPayload(99),
            new CrdtTimestamp(1000),
            1
        );

        var typeInfo = (JsonTypeInfo<CrdtOperation>)serializerOptions.GetTypeInfo(typeof(CrdtOperation));
        var json = JsonSerializer.Serialize(operation, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        json.ShouldContain("\"Value\":{\"Value\":99,\"$type\":\"custom-payload\"}");
        deserialized.Value.ShouldBeOfType<CustomPayload>();
        ((CustomPayload)deserialized.Value!).Value.ShouldBe(99);
    }

    [Fact]
    public void Serialize_WithUnregisteredCustomPayload_ShouldThrowException()
    {
        var operation = new CrdtOperation(
            Guid.NewGuid(),
            "r1",
            "/path",
            OperationType.Upsert,
            new UnregisteredPayload("test"),
            new CrdtTimestamp(1000),
            1
        );

        var typeInfo = (JsonTypeInfo<CrdtOperation>)serializerOptions.GetTypeInfo(typeof(CrdtOperation));

        Should.Throw<NotSupportedException>(() => JsonSerializer.Serialize(operation, typeInfo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Register_WithInvalidDiscriminator_ShouldThrowArgumentException(string? discriminator)
    {
        Should.Throw<ArgumentException>(() => CrdtTypeRegistry.Register(discriminator!, typeof(UnregisteredPayload)))
            .Message.ShouldContain("Discriminator cannot be null or whitespace.");
    }
}