namespace Modern.CRDT.UnitTests.Services.Strategies;

using Microsoft.Extensions.Options;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;

public sealed class LwwStrategyTests
{
    private sealed record TestModel { public int Value { get; init; } }

    private readonly LwwStrategy strategy;
    private readonly Mock<IJsonCrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    public LwwStrategyTests()
    {
        strategy = new LwwStrategy(Options.Create(new CrdtOptions { ReplicaId = Guid.NewGuid().ToString() }));
    }

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
        op.Timestamp.ShouldBe(new EpochTimestamp(200L));
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
    public void ApplyOperation_Upsert_ShouldUpdateValue()
    {
        // Arrange
        var rootNode = new JsonObject { ["value"] = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.value", OperationType.Upsert, JsonValue.Create(20), new EpochTimestamp(200L));

        // Act
        strategy.ApplyOperation(rootNode, operation);

        // Assert
        rootNode["value"]!.GetValue<int>().ShouldBe(20);
    }

    [Fact]
    public void ApplyOperation_Remove_ShouldRemoveValue()
    {
        // Arrange
        var rootNode = new JsonObject { ["value"] = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.value", OperationType.Remove, null, new EpochTimestamp(200L));

        // Act
        strategy.ApplyOperation(rootNode, operation);

        // Assert
        rootNode.ContainsKey("value").ShouldBeFalse();
    }
}