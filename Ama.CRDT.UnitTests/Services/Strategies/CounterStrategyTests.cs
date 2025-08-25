namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class CounterStrategyTests
{
    private sealed class TestModel { public int Score { get; set; } }
    
    private readonly CounterStrategy strategy;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
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
        var path = "$.score";
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Score))!;
        var expectedTimestamp = new EpochTimestamp(12345);
        mockTimestampProvider.Setup(p => p.Now()).Returns(expectedTimestamp);

        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, path, property, original, modified, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        operations.ShouldHaveSingleItem();
        var op = operations.First();
        op.Type.ShouldBe(OperationType.Increment);
        op.JsonPath.ShouldBe(path);
        op.Value.ShouldNotBeNull();
        op.Value.ShouldBe((decimal)delta);
        op.Timestamp.ShouldBe(expectedTimestamp);
    }

    [Theory]
    [InlineData(10, 5, 15)]
    [InlineData(10, -5, 5)]
    [InlineData(0, 5, 5)]
    public void ApplyOperation_ShouldIncrementValue_Correctly(int initial, int increment, int expected)
    {
        // Arrange
        var model = new TestModel { Score = initial };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.score", OperationType.Increment, (decimal)increment, new EpochTimestamp(2L));

        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);

        // Assert
        model.Score.ShouldBe(expected);
    }
    
    [Fact]
    public void ApplyOperation_ShouldSetInitialValue_WhenPropertyDoesNotExist()
    {
        // Arrange
        var model = new TestModelWithNoScore();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Score", OperationType.Increment, 5m, new EpochTimestamp(1L));

        // Act & Assert: This should not throw because the property doesn't exist.
        // The helper will return nulls and the strategy will exit gracefully.
        Should.NotThrow(() => strategy.ApplyOperation(model, new CrdtMetadata(), operation));
    }
    
    private sealed class TestModelWithNoScore { public string? Name { get; set; } }

    [Fact]
    public void ApplyOperation_ShouldThrow_WhenOperationTypeIsNotIncrement()
    {
        // Arrange
        var model = new TestModel();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.score", OperationType.Upsert, 5m, new EpochTimestamp(1L));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(model, new CrdtMetadata(), operation));
    }
}