namespace Modern.CRDT.UnitTests.Services.Strategies;

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
using static Modern.CRDT.Services.Strategies.ArrayLcsStrategy;
using static Modern.CRDT.Services.Strategies.JsonNodeComparerProvider;

public sealed class ArrayLcsStrategyTests
{
    private sealed record TestModel
    {
        public List<NestedModel>? Items { get; init; }
    }

    private sealed record NestedModel
    {
        public int Id { get; init; }
        public string? Value { get; init; }
    }
    
    private sealed class NestedModelIdComparer : IJsonNodeComparer
    {
        public bool CanCompare(Type type)
        {
            return type == typeof(NestedModel);
        }

        public bool Equals(JsonNode? x, JsonNode? y)
        {
            if (x is not JsonObject objX || y is not JsonObject objY)
            {
                return JsonNode.DeepEquals(x, y);
            }

            if (!objX.TryGetPropertyValue("id", out var idX) || !objY.TryGetPropertyValue("id", out var idY))
            {
                return false;
            }

            return JsonNode.DeepEquals(idX, idY);
        }

        public int GetHashCode(JsonNode obj)
        {
            if (obj is JsonObject objX && objX.TryGetPropertyValue("id", out var idNode))
            {
                return idNode?.ToJsonString().GetHashCode() ?? 0;
            }
            return obj.ToJsonString().GetHashCode();
        }
    }

    private readonly Mock<IJsonCrdtPatcher> mockPatcher = new();
    private readonly Mock<IJsonNodeComparerProvider> mockComparerProvider = new();
    private readonly ArrayLcsStrategy strategy;

    public ArrayLcsStrategyTests()
    {
        strategy = new ArrayLcsStrategy(mockComparerProvider.Object);
        mockComparerProvider
            .Setup(p => p.GetComparer(It.IsAny<Type>()))
            .Returns<IEqualityComparer<JsonNode>>(null);
    }

    [Fact]
    public void GeneratePatch_WithCustomIdComparer_WhenObjectInArrayIsModified_ShouldCallPatcherDifferentiateObject()
    {
        // Arrange
        mockComparerProvider
            .Setup(p => p.GetComparer(typeof(NestedModel)))
            .Returns(new NestedModelIdComparer());

        var operations = new List<CrdtOperation>();
        var path = "$.items";
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Items))!;

        var originalValue = new JsonArray
        {
            new JsonObject { ["id"] = 1, ["value"] = "one" },
            new JsonObject { ["id"] = 2, ["value"] = "two" }
        };
        var modifiedValue = new JsonArray
        {
            new JsonObject { ["id"] = 1, ["value"] = "one" },
            new JsonObject { ["id"] = 2, ["value"] = "two-updated" }
        };

        var ts = 12345L;
        var originalMeta = new JsonArray { new JsonObject(), new JsonObject { ["value"] = ts } };
        var modifiedMeta = new JsonArray { new JsonObject(), new JsonObject { ["value"] = ts + 1 } };

        mockPatcher
            .Setup(p => p.DifferentiateObject(It.IsAny<string>(), It.IsAny<System.Type>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<List<CrdtOperation>>()))
            .Callback<string, System.Type, JsonObject, JsonObject, JsonObject, JsonObject, List<CrdtOperation>>((itemPath, _, _, _, toObject, _, ops) =>
            {
                ops.Add(new CrdtOperation($"{itemPath}.value", OperationType.Upsert, toObject["value"]!.DeepClone(), 0));
            });
        
        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, path, property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        // Assert
        mockPatcher.Verify(p => p.DifferentiateObject(
            "$.items[1]",
            typeof(NestedModel),
            It.Is<JsonObject>(o => o["id"]!.GetValue<int>() == 2),
            It.IsAny<JsonObject>(),
            It.Is<JsonObject>(o => o["id"]!.GetValue<int>() == 2),
            It.IsAny<JsonObject>(),
            operations
        ), Times.Once);
        
        operations.ShouldHaveSingleItem();
        operations.Single().JsonPath.ShouldBe("$.items[1].value");
    }
    
    [Fact]
    public void ApplyOperation_Upsert_ShouldInsertItemIntoArray()
    {
        // Arrange
        var rootNode = new JsonObject { ["items"] = new JsonArray("a", "c") };
        var operation = new CrdtOperation("$.items[1]", OperationType.Upsert, JsonValue.Create("b"), 1L);

        // Act
        strategy.ApplyOperation(rootNode, operation);

        // Assert
        var array = rootNode["items"]!.AsArray();
        array.Count.ShouldBe(3);
        array[0]!.GetValue<string>().ShouldBe("a");
        array[1]!.GetValue<string>().ShouldBe("b");
        array[2]!.GetValue<string>().ShouldBe("c");
    }

    [Fact]
    public void ApplyOperation_Remove_ShouldRemoveItemFromArray()
    {
        // Arrange
        var rootNode = new JsonObject { ["items"] = new JsonArray("a", "b", "c") };
        var operation = new CrdtOperation("$.items[1]", OperationType.Remove, null, 1L);

        // Act
        strategy.ApplyOperation(rootNode, operation);

        // Assert
        var array = rootNode["items"]!.AsArray();
        array.Count.ShouldBe(2);
        array[0]!.GetValue<string>().ShouldBe("a");
        array[1]!.GetValue<string>().ShouldBe("c");
    }
    
    #region Diff Method Tests

    [Fact]
    public void Diff_WhenArraysAreIdentical_ShouldReturnAllMatches()
    {
        // Arrange
        var from = new JsonArray(1, 2, 3);
        var to = new JsonArray(1, 2, 3);

        // Act
        var diff = strategy.Diff(from, to, JsonNodeDeepEqualityComparer.Instance);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Match, 0, 0),
            new(LcsDiffEntryType.Match, 1, 1),
            new(LcsDiffEntryType.Match, 2, 2)
        });
    }

    [Fact]
    public void Diff_WhenItemIsAdded_ShouldReturnAddOperation()
    {
        // Arrange
        var from = new JsonArray("A", "C");
        var to = new JsonArray("A", "B", "C");

        // Act
        var diff = strategy.Diff(from, to, JsonNodeDeepEqualityComparer.Instance);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Match, 0, 0),
            new(LcsDiffEntryType.Add, -1, 1),
            new(LcsDiffEntryType.Match, 1, 2)
        });
    }

    [Fact]
    public void Diff_WhenItemIsRemoved_ShouldReturnRemoveOperation()
    {
        // Arrange
        var from = new JsonArray("A", "B", "C");
        var to = new JsonArray("A", "C");

        // Act
        var diff = strategy.Diff(from, to, JsonNodeDeepEqualityComparer.Instance);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Match, 0, 0),
            new(LcsDiffEntryType.Remove, 1, -1),
            new(LcsDiffEntryType.Match, 2, 1)
        });
    }
    
    #endregion
}