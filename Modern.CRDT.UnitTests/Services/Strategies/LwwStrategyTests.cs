namespace Modern.CRDT.UnitTests.Services.Strategies;

using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

public sealed class LwwStrategyTests
{
    private sealed record TestModel { public int Value { get; init; } }

    private readonly LwwStrategy strategy = new();
    private readonly Mock<IJsonCrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    [Fact]
    public void GeneratePatch_WhenModifiedIsNewer_ShouldGenerateUpsert()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(20);
        var originalMeta = JsonValue.Create(100L);
        var modifiedMeta = JsonValue.Create(200L);
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.value");
        op.Value!.GetValue<int>().ShouldBe(20);
        op.Timestamp.ShouldBe(200L);
    }
    
    [Fact]
    public void GeneratePatch_WhenModifiedIsOlder_ShouldGenerateNothing()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(20);
        var originalMeta = JsonValue.Create(200L);
        var modifiedMeta = JsonValue.Create(100L);
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void GeneratePatch_WhenModifiedIsSameTimestamp_ShouldGenerateNothing()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(20);
        var originalMeta = JsonValue.Create(200L);
        var modifiedMeta = JsonValue.Create(200L);
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void GeneratePatch_WhenValueIsRemovedAndTimestampIsNewer_ShouldGenerateRemove()
    {
        var originalValue = JsonValue.Create(10);
        JsonNode? modifiedValue = null;
        var originalMeta = JsonValue.Create(100L);
        var modifiedMeta = JsonValue.Create(200L);
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);
        
        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Remove);
        op.JsonPath.ShouldBe("$.value");
        op.Value.ShouldBeNull();
        op.Timestamp.ShouldBe(200L);
    }

    [Fact]
    public void GeneratePatch_WhenValueIsAdded_ShouldGenerateUpsert()
    {
        JsonNode? originalValue = null;
        var modifiedValue = JsonValue.Create("new");
        JsonNode? originalMeta = null;
        var modifiedMeta = JsonValue.Create(200L);
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.value");
        op.Value!.GetValue<string>().ShouldBe("new");
        op.Timestamp.ShouldBe(200L);
    }
    
    [Fact]
    public void GeneratePatch_WhenValuesAreIdentical_ShouldGenerateNothing()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(10);
        var originalMeta = JsonValue.Create(100L);
        var modifiedMeta = JsonValue.Create(200L);
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        operations.ShouldBeEmpty();
    }
}