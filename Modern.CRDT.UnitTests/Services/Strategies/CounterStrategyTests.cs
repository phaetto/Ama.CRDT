namespace Modern.CRDT.UnitTests.Services.Strategies;

using Microsoft.Extensions.Options;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

public sealed class CounterStrategyTests
{
    private sealed record TestModel { public int Score { get; init; } }
    
    private readonly CounterStrategy strategy;
    private readonly Mock<IJsonCrdtPatcher> mockPatcher = new();
    private readonly Mock<ICrdtTimestampProvider> mockTimestampProvider = new();

    public CounterStrategyTests()
    {
        strategy = new CounterStrategy(mockTimestampProvider.Object, Options.Create(new CrdtOptions { ReplicaId = Guid.NewGuid().ToString() }));
    }

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
        var expectedTimestamp = new EpochTimestamp(12345);
        mockTimestampProvider.Setup(p => p.Now()).Returns(expectedTimestamp);

        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, path, property, originalNode, modifiedNode, null, null);

        // Assert
        operations.ShouldHaveSingleItem();
        var op = operations.First();
        op.Type.ShouldBe(OperationType.Increment);
        op.JsonPath.ShouldBe(path);
        op.Value.ShouldNotBeNull();
        op.Value.GetValue<decimal>().ShouldBe(delta);
        op.Timestamp.ShouldBe(expectedTimestamp);
    }

    [Theory]
    [InlineData(10, 5, 15)]
    [InlineData(10, -5, 5)]
    [InlineData(0, 5, 5)]
    public void ApplyOperation_ShouldIncrementValue_Correctly(int initial, int increment, int expected)
    {
        // Arrange
        var rootNode = new JsonObject { ["Score"] = initial };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Score", OperationType.Increment, JsonValue.Create(increment), new EpochTimestamp(2L));

        // Act
        strategy.ApplyOperation(rootNode, operation);

        // Assert
        rootNode["Score"].ShouldNotBeNull();
        rootNode["Score"].GetValue<decimal>().ShouldBe(expected);
    }
    
    [Fact]
    public void ApplyOperation_ShouldSetInitialValue_WhenPropertyDoesNotExist()
    {
        // Arrange
        var rootNode = new JsonObject { ["Id"] = "A" };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Score", OperationType.Increment, JsonValue.Create(5), new EpochTimestamp(1L));

        // Act
        strategy.ApplyOperation(rootNode, operation);

        // Assert
        rootNode["Score"].ShouldNotBeNull();
        rootNode["Score"].GetValue<decimal>().ShouldBe(5);
    }
    
    [Fact]
    public void ApplyOperation_ShouldThrow_WhenTargetIsNotNumeric()
    {
        // Arrange
        var rootNode = new JsonObject { ["Score"] = "not a number" };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Score", OperationType.Increment, JsonValue.Create(5), new EpochTimestamp(1L));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(rootNode, operation));
    }

    [Fact]
    public void ApplyOperation_ShouldThrow_WhenOperationTypeIsNotIncrement()
    {
        // Arrange
        var rootNode = new JsonObject();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Score", OperationType.Upsert, JsonValue.Create(5), new EpochTimestamp(1L));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(rootNode, operation));
    }
}