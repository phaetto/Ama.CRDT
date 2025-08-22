namespace Modern.CRDT.UnitTests.Services.Strategies;

using Modern.CRDT.Services.Strategies;
using System.Text.Json.Nodes;
using Shouldly;
using System.Linq;
using Modern.CRDT.Models;
using System;
using System.Collections.Generic;
using Moq;
using Modern.CRDT.Services;

public sealed class CounterStrategyTests
{
    private sealed record TestModel { public int Score { get; init; } }
    
    private readonly CounterStrategy strategy = new();
    private readonly Mock<IJsonCrdtPatcher> mockPatcher = new();

    [Theory]
    [InlineData(10, 15, 5)]
    [InlineData(10, 5, -5)]
    [InlineData(-5, 5, 10)]
    public void GeneratePatch_ShouldCreateIncrementOperation_WhenValueChanges(int original, int modified, int delta)
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var originalNode = JsonValue.Create(original);
        var modifiedNode = JsonValue.Create(modified);
        var path = "$.Score";
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Score))!;

        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, path, property, originalNode, modifiedNode, null, null);

        // Assert
        operations.ShouldHaveSingleItem();
        var op = operations.First();
        op.Type.ShouldBe(OperationType.Increment);
        op.JsonPath.ShouldBe(path);
        op.Value.ShouldNotBeNull();
        op.Value.GetValue<decimal>().ShouldBe(delta);
    }

    [Fact]
    public void GeneratePatch_ShouldReturnEmpty_WhenValueIsUnchanged()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var originalNode = JsonValue.Create(10);
        var modifiedNode = JsonValue.Create(10);
        var path = "$.Score";
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Score))!;

        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, path, property, originalNode, modifiedNode, null, null);

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void GeneratePatch_ShouldThrow_WhenValuesAreNotNumeric()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var originalNode = JsonValue.Create(10);
        var modifiedNode = JsonValue.Create("not a number");
        var path = "$.Score";
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Score))!;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.GeneratePatch(mockPatcher.Object, operations, path, property, originalNode, modifiedNode, null, null));
    }

    [Theory]
    [InlineData(10, 5, 15)]
    [InlineData(10, -5, 5)]
    [InlineData(0, 5, 5)]
    public void ApplyOperation_ShouldIncrementValue_Correctly(int initial, int increment, int expected)
    {
        // Arrange
        var rootNode = new JsonObject { ["Score"] = initial };
        var metaNode = new JsonObject { ["Score"] = 1L };
        var operation = new CrdtOperation("$.Score", OperationType.Increment, JsonValue.Create(increment), 2L);

        // Act
        strategy.ApplyOperation(rootNode, metaNode, operation);

        // Assert
        rootNode["Score"].ShouldNotBeNull();
        rootNode["Score"].GetValue<decimal>().ShouldBe(expected);
        metaNode["Score"]!.GetValue<long>().ShouldBe(2L);
    }
    
    [Fact]
    public void ApplyOperation_ShouldSetInitialValue_WhenPropertyDoesNotExist()
    {
        // Arrange
        var rootNode = new JsonObject { ["Id"] = "A" };
        var metaNode = new JsonObject { ["Id"] = "A" };
        var operation = new CrdtOperation("$.Score", OperationType.Increment, JsonValue.Create(5), 1L);

        // Act
        strategy.ApplyOperation(rootNode, metaNode, operation);

        // Assert
        rootNode["Score"].ShouldNotBeNull();
        rootNode["Score"].GetValue<decimal>().ShouldBe(5);
        metaNode["Score"]!.GetValue<long>().ShouldBe(1L);
    }
    
    [Fact]
    public void ApplyOperation_ShouldThrow_WhenTargetIsNotNumeric()
    {
        // Arrange
        var rootNode = new JsonObject { ["Score"] = "not a number" };
        var metaNode = new JsonObject();
        var operation = new CrdtOperation("$.Score", OperationType.Increment, JsonValue.Create(5), 1L);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(rootNode, metaNode, operation));
    }

    [Fact]
    public void ApplyOperation_ShouldThrow_WhenOperationTypeIsNotIncrement()
    {
        // Arrange
        var rootNode = new JsonObject();
        var metaNode = new JsonObject();
        var operation = new CrdtOperation("$.Score", OperationType.Upsert, JsonValue.Create(5), 1L);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(rootNode, metaNode, operation));
    }
}