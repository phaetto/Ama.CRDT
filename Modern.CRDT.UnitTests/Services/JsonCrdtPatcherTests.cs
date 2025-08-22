namespace Modern.CRDT.UnitTests.Services;

using Modern.CRDT.Attributes;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

public sealed class JsonCrdtPatcherTests
{
    private sealed record TestModel
    {
        public string? Name { get; init; }

        [CrdtCounter]
        public int Likes { get; init; }

        public NestedModel? Nested { get; init; }

        public long Unchanged { get; init; }
    }

    private sealed record NestedModel
    {
        [LwwStrategy]
        public string? Value { get; init; }
    }

    private readonly IJsonCrdtPatcher patcher;

    public JsonCrdtPatcherTests()
    {
        var lwwStrategy = new LwwStrategy();
        var counterStrategy = new CounterStrategy();
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        patcher = new JsonCrdtPatcher(strategyManager);
    }

    [Fact]
    public void GeneratePatch_WhenNoChanges_ShouldReturnEmptyPatch()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Name = "Test", Likes = 10, Unchanged = 123 };
        var metadata = JsonNode.Parse($"{{\"name\":{now},\"likes\":{now},\"unchanged\":{now}}}");
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
        var fromMeta = JsonNode.Parse($"{{\"name\":{ts1},\"likes\":{ts1},\"unchanged\":{ts1}}}");
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Updated", Likes = 15, Unchanged = 123 };
        var toMeta = JsonNode.Parse($"{{\"name\":{ts2},\"likes\":{ts2},\"unchanged\":{ts1}}}");
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(2);

        var lwwOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.name");
        lwwOp.Type.ShouldBe(OperationType.Upsert);
        lwwOp.Value.AsValue().GetValue<string>().ShouldBe("Updated");
        lwwOp.Timestamp.ShouldBe(ts2);

        var counterOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.likes");
        counterOp.Type.ShouldBe(OperationType.Increment);
        counterOp.Value.AsValue().GetValue<decimal>().ShouldBe(10); // 15 - 5
    }

    [Fact]
    public void GeneratePatch_WithNestedObjectChanges_ShouldGenerateCorrectPath()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Nested = new NestedModel { Value = "Nested Original" } };
        var fromMeta = JsonNode.Parse($"{{\"nested\":{{\"value\":{ts1}}}}}");
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Nested = new NestedModel { Value = "Nested Updated" } };
        var toMeta = JsonNode.Parse($"{{\"nested\":{{\"value\":{ts2}}}}}");
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(1);

        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.AsValue().GetValue<string>().ShouldBe("Nested Updated");
        op.Timestamp.ShouldBe(ts2);
    }
    
    [Fact]
    public void GeneratePatch_WhenNestedObjectIsAdded_ShouldGeneratePatchForProperties()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Name = "Test" };
        var fromMeta = JsonNode.Parse($"{{\"name\":{ts1}}}");
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Test", Nested = new NestedModel { Value = "Added" } };
        var toMeta = JsonNode.Parse($"{{\"name\":{ts1},\"nested\":{{\"value\":{ts2}}}}}");
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);
        
        // Assert
        patch.Operations.Count.ShouldBe(1);
        
        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.AsValue().GetValue<string>().ShouldBe("Added");
        op.Timestamp.ShouldBe(ts2);
    }
    
    [Fact]
    public void GeneratePatch_WhenNestedObjectIsRemoved_ShouldGenerateRemoveOperationsForProperties()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Name = "Test", Nested = new NestedModel { Value = "Existing" } };
        var fromMeta = JsonNode.Parse($"{{\"name\":{ts1},\"nested\":{{\"value\":{ts1}}}}}");
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Test", Nested = null };
        var toMeta = JsonNode.Parse($"{{\"name\":{ts1},\"nested\":{{\"value\":{ts2}}}}}");
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(1);
        
        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBeNull();
        op.Timestamp.ShouldBe(ts2);
    }
    
    [Fact]
    public void GeneratePatch_WhenPropertyWithoutAttribute_ShouldUseLwwStrategyAsDefault()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromModel = new TestModel { Name = "Original" };
        var fromMeta = JsonNode.Parse($"{{\"name\":{ts1}}}");
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = ts1 + 100;
        var toModel = new TestModel { Name = "Updated" };
        var toMeta = JsonNode.Parse($"{{\"name\":{ts2}}}");
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);
        
        // Assert
        patch.Operations.Count.ShouldBe(1);
        
        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.name");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Timestamp.ShouldBe(ts2);
    }
}