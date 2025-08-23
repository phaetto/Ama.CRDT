using Microsoft.Extensions.Options;
using Modern.CRDT.Attributes;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Shouldly;
using System.Text.Json.Nodes;
using Xunit;

namespace Modern.CRDT.UnitTests.Services;

public sealed class JsonCrdtPatcherTests
{
    private sealed record TestModel
    {
        public string? Name { get; init; }

        [CrdtCounter]
        public int Likes { get; init; }

        public NestedModel? Nested { get; init; }

        public long Unchanged { get; init; }

        public List<string>? Tags { get; init; }
    }

    private sealed record NestedModel
    {
        [LwwStrategy]
        public string? Value { get; init; }
    }

    private readonly IJsonCrdtPatcher patcher;

    public JsonCrdtPatcherTests()
    {
        var options = Options.Create(new CrdtOptions { ReplicaId = "test-patcher" });
        var timestampProvider = new EpochTimestampProvider();
        var lwwStrategy = new LwwStrategy(options);
        var counterStrategy = new CounterStrategy(timestampProvider, options);
        var comparerProvider = new JsonNodeComparerProvider(Enumerable.Empty<IJsonNodeComparer>());
        var arrayStrategy = new ArrayLcsStrategy(comparerProvider, timestampProvider, options);
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        patcher = new JsonCrdtPatcher(strategyManager);
    }

    [Fact]
    public void GeneratePatch_WhenNoChanges_ShouldReturnEmptyPatch()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Name = "Test", Likes = 10, Unchanged = 123 };
        var metadata = new CrdtMetadata();
        metadata.Lww["$.name"] = new EpochTimestamp(now);
        metadata.Lww["$.likes"] = new EpochTimestamp(now);
        metadata.Lww["$.unchanged"] = new EpochTimestamp(now);

        var from = new CrdtDocument<TestModel>(model, metadata);
        var to = new CrdtDocument<TestModel>(model, metadata);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void GeneratePatch_WithMixedStrategyProperties_ShouldGenerateCorrectOperations()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Name = "Original", Likes = 5, Unchanged = 123 };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.name"] = new EpochTimestamp(ts1);
        fromMeta.Lww["$.likes"] = new EpochTimestamp(ts1);
        fromMeta.Lww["$.unchanged"] = new EpochTimestamp(ts1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Updated", Likes = 15, Unchanged = 123 };
        var toMeta = new CrdtMetadata();
        toMeta.Lww["$.name"] = new EpochTimestamp(ts2);
        toMeta.Lww["$.likes"] = new EpochTimestamp(ts2);
        toMeta.Lww["$.unchanged"] = new EpochTimestamp(ts1);
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(2);

        var lwwOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.name");
        lwwOp!.Type.ShouldBe(OperationType.Upsert);
        lwwOp.Value!.AsValue().GetValue<string>().ShouldBe("Updated");
        lwwOp.Timestamp.ShouldBe(new EpochTimestamp(ts2));

        var counterOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.likes");
        counterOp!.Type.ShouldBe(OperationType.Increment);
        counterOp.Value!.AsValue().GetValue<decimal>().ShouldBe(10); // 15 - 5
    }

    [Fact]
    public void GeneratePatch_WithArrayChanges_ShouldGenerateLcsPatch()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Tags = ["a", "b", "c"] };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.tags[0]"] = new EpochTimestamp(ts1);
        fromMeta.Lww["$.tags[1]"] = new EpochTimestamp(ts1);
        fromMeta.Lww["$.tags[2]"] = new EpochTimestamp(ts1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Tags = ["a", "x", "c"] }; // "b" removed, "x" inserted
        var toMeta = new CrdtMetadata();
        toMeta.Lww["$.tags[0]"] = new EpochTimestamp(ts1);
        toMeta.Lww["$.tags[1]"] = new EpochTimestamp(ts2);
        toMeta.Lww["$.tags[2]"] = new EpochTimestamp(ts1);
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(2);

        var removeOp = patch.Operations.SingleOrDefault(o => o.Type == OperationType.Remove);
        removeOp.JsonPath.ShouldBe("$.tags[1]");

        var upsertOp = patch.Operations.SingleOrDefault(o => o.Type == OperationType.Upsert);
        upsertOp.JsonPath.ShouldBe("$.tags[1]");
        upsertOp.Value!.AsValue().GetValue<string>().ShouldBe("x");
    }

    [Fact]
    public void GeneratePatch_WithNestedObjectChanges_ShouldGenerateCorrectPath()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Nested = new NestedModel { Value = "Nested Original" } };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.nested.value"] = new EpochTimestamp(ts1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Nested = new NestedModel { Value = "Nested Updated" } };
        var toMeta = new CrdtMetadata();
        toMeta.Lww["$.nested.value"] = new EpochTimestamp(ts2);
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(1);

        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value!.AsValue().GetValue<string>().ShouldBe("Nested Updated");
        op.Timestamp.ShouldBe(new EpochTimestamp(ts2));
    }

    [Fact]
    public void GeneratePatch_WhenNestedObjectIsAdded_ShouldGeneratePatchForProperties()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Name = "Test" };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.name"] = new EpochTimestamp(ts1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Test", Nested = new NestedModel { Value = "Added" } };
        var toMeta = new CrdtMetadata();
        toMeta.Lww["$.name"] = new EpochTimestamp(ts1);
        toMeta.Lww["$.nested.value"] = new EpochTimestamp(ts2);
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(1);

        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value!.AsValue().GetValue<string>().ShouldBe("Added");
        op.Timestamp.ShouldBe(new EpochTimestamp(ts2));
    }

    [Fact]
    public void GeneratePatch_WhenNestedObjectIsRemoved_ShouldGenerateRemoveOperationsForProperties()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Name = "Test", Nested = new NestedModel { Value = "Existing" } };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.name"] = new EpochTimestamp(ts1);
        fromMeta.Lww["$.nested.value"] = new EpochTimestamp(ts1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Test", Nested = null };
        var toMeta = new CrdtMetadata(); // Meta for 'to' state doesn't need old value, but LWW needs a timestamp for the 'remove' action.
        toMeta.Lww["$.name"] = new EpochTimestamp(ts1);
        toMeta.Lww["$.nested.value"] = new EpochTimestamp(ts2);
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(1);

        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBeNull();
        op.Timestamp.ShouldBe(new EpochTimestamp(ts2));
    }

    [Fact]
    public void GeneratePatch_WhenPropertyWithoutAttribute_ShouldUseLwwStrategyAsDefault()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Name = "Original" };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.name"] = new EpochTimestamp(ts1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Updated" };
        var toMeta = new CrdtMetadata();
        toMeta.Lww["$.name"] = new EpochTimestamp(ts2);
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(1);

        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.name");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Timestamp.ShouldBe(new EpochTimestamp(ts2));
    }
}