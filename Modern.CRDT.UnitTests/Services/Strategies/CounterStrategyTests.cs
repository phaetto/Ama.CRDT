namespace Modern.CRDT.UnitTests.Services.Strategies;

using Modern.CRDT.Services.Strategies;
using System.Text.Json.Nodes;
using Shouldly;
using System.Linq;
using Modern.CRDT.Models;
using System;

public sealed class CounterStrategyTests
{
    private readonly CounterStrategy strategy = new();

    [Theory]
    [InlineData(10, 15, 5)]
    [InlineData(10, 5, -5)]
    [InlineData(-5, 5, 10)]
    public void GeneratePatch_ShouldCreateIncrementOperation_WhenValueChanges(int original, int modified, int delta)
    {
        // Arrange
        var originalNode = JsonValue.Create(original);
        var modifiedNode = JsonValue.Create(modified);
        var path = "$.Score";

        // Act
        var operations = strategy.GeneratePatch(path, originalNode, modifiedNode, null, null).ToList();

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
        var originalNode = JsonValue.Create(10);
        var modifiedNode = JsonValue.Create(10);
        var path = "$.Score";

        // Act
        var operations = strategy.GeneratePatch(path, originalNode, modifiedNode, null, null).ToList();

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void GeneratePatch_ShouldThrow_WhenValuesAreNotNumeric()
    {
        // Arrange
        var originalNode = JsonValue.Create(10);
        var modifiedNode = JsonValue.Create("not a number");
        var path = "$.Score";

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.GeneratePatch(path, originalNode, modifiedNode, null, null).ToList());
    }

    [Theory]
    [InlineData(10, 5, 15)]
    [InlineData(10, -5, 5)]
    [InlineData(0, 5, 5)]
    public void ApplyOperation_ShouldIncrementValue_Correctly(int initial, int increment, int expected)
    {
        // Arrange
        var rootNode = new JsonObject
        {
            ["Score"] = initial
        };
        var operation = new CrdtOperation("$.Score", OperationType.Increment, JsonValue.Create(increment), 0);

        // Act
        strategy.ApplyOperation(rootNode, null, operation);

        // Assert
        rootNode["Score"].ShouldNotBeNull();
        rootNode["Score"].GetValue<decimal>().ShouldBe(expected);
    }
    
    [Fact]
    public void ApplyOperation_ShouldSetInitialValue_WhenPropertyDoesNotExist()
    {
        // Arrange
        var rootNode = new JsonObject
        {
            ["Id"] = "A"
        };
        var operation = new CrdtOperation("$.Score", OperationType.Increment, JsonValue.Create(5), 0);

        // Act
        strategy.ApplyOperation(rootNode, null, operation);

        // Assert
        rootNode["Score"].ShouldNotBeNull();
        rootNode["Score"].GetValue<decimal>().ShouldBe(5);
    }
    
    [Fact]
    public void ApplyOperation_ShouldThrow_WhenTargetIsNotNumeric()
    {
        // Arrange
        var rootNode = new JsonObject
        {
            ["Score"] = "not a number"
        };
        var operation = new CrdtOperation("$.Score", OperationType.Increment, JsonValue.Create(5), 0);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(rootNode, null, operation));
    }

    [Fact]
    public void ApplyOperation_ShouldThrow_WhenOperationTypeIsNotIncrement()
    {
        // Arrange
        var rootNode = new JsonObject();
        var operation = new CrdtOperation("$.Score", OperationType.Upsert, JsonValue.Create(5), 0);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(rootNode, null, operation));
    }
}