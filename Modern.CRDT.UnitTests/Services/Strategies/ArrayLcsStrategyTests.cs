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
using static Modern.CRDT.Services.Strategies.ArrayLcsStrategy;

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
    
    private sealed class NestedModelIdComparer : IEqualityComparer<JsonNode>
    {
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
    
    [Fact]
    public void GeneratePatch_WithCustomIdComparer_WhenObjectInArrayIsModified_ShouldCallPatcherDifferentiateObject()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy(new NestedModelIdComparer());
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
    public void GeneratePatch_WithCustomIdComparer_WithMixedAddRemoveUpdate_ShouldGenerateAndCallCorrectly()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy(new NestedModelIdComparer());
        var operations = new List<CrdtOperation>();
        var path = "$.items";
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Items))!;

        var originalValue = new JsonArray
        {
            new JsonObject { ["id"] = 1, ["value"] = "A" },
            new JsonObject { ["id"] = 2, ["value"] = "B" },
            new JsonObject { ["id"] = 3, ["value"] = "C" },
        };
        var modifiedValue = new JsonArray
        {
            new JsonObject { ["id"] = 1, ["value"] = "A" },
            new JsonObject { ["id"] = 3, ["value"] = "C-updated" },
            new JsonObject { ["id"] = 4, ["value"] = "D" },
        };
        
        mockPatcher
            .Setup(p => p.DifferentiateObject(It.IsAny<string>(), It.IsAny<System.Type>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<List<CrdtOperation>>()))
            .Callback<string, System.Type, JsonObject, JsonObject, JsonObject, JsonObject, List<CrdtOperation>>((itemPath, _, _, _, toObject, _, ops) =>
            {
                ops.Add(new CrdtOperation($"{itemPath}.value", OperationType.Upsert, toObject["value"]!.DeepClone(), 0));
            });

        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, path, property, originalValue, modifiedValue, null, null);
        
        // Assert
        mockPatcher.Verify(p => p.DifferentiateObject(
            It.Is<string>(s => s == "$.items[2]"), // Path to original item C
            typeof(NestedModel),
            It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(),
            It.IsAny<List<CrdtOperation>>()),
            Times.Once);

        operations.Count.ShouldBe(3);

        var removeOp = operations.Single(op => op.Type == OperationType.Remove);
        removeOp.JsonPath.ShouldBe("$.items[1]");

        var addOp = operations.Single(op => op.Type == OperationType.Upsert && op.JsonPath == "$.items[2]");
        addOp.Value!.AsObject()["value"]!.GetValue<string>().ShouldBe("D");

        var updateOp = operations.Single(op => op.JsonPath == "$.items[2].value");
        updateOp.Value!.GetValue<string>().ShouldBe("C-updated");
    }
    
    #region Diff Method Tests

    [Fact]
    public void Diff_WhenArraysAreIdentical_ShouldReturnAllMatches()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray(1, 2, 3);
        var to = new JsonArray(1, 2, 3);

        // Act
        var diff = strategy.Diff(from, to);

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
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray("A", "C");
        var to = new JsonArray("A", "B", "C");

        // Act
        var diff = strategy.Diff(from, to);

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
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray("A", "B", "C");
        var to = new JsonArray("A", "C");

        // Act
        var diff = strategy.Diff(from, to);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Match, 0, 0),
            new(LcsDiffEntryType.Remove, 1, -1),
            new(LcsDiffEntryType.Match, 2, 1)
        });
    }

    [Fact]
    public void Diff_WithMixedOperations_ShouldReturnCorrectDiff()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray("A", "B", "C", "D", "E");
        var to = new JsonArray("A", "C", "F", "E");

        // Act
        var diff = strategy.Diff(from, to);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Match, 0, 0),
            new(LcsDiffEntryType.Remove, 1, -1),
            new(LcsDiffEntryType.Match, 2, 1),
            new(LcsDiffEntryType.Remove, 3, -1),
            new(LcsDiffEntryType.Add, -1, 2),
            new(LcsDiffEntryType.Match, 4, 3)
        });
    }

    [Fact]
    public void Diff_WithDefaultComparer_WhenObjectsAreDifferent_ShouldReturnRemovesAndAdds()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray(
            new JsonObject { ["id"] = 1, ["value"] = "one" },
            new JsonObject { ["id"] = 2, ["value"] = "two" }
        );
        var to = new JsonArray(
            new JsonObject { ["id"] = 1, ["value"] = "one-updated" },
            new JsonObject { ["id"] = 3, ["value"] = "three" }
        );
        
        // Act
        var diff = strategy.Diff(from, to);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Remove, 0, -1),
            new(LcsDiffEntryType.Remove, 1, -1),
            new(LcsDiffEntryType.Add, -1, 0),
            new(LcsDiffEntryType.Add, -1, 1)
        });
    }
    
    [Fact]
    public void Diff_WithCustomIdComparer_WhenObjectIsModified_ShouldReturnMatch()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy(new NestedModelIdComparer());
        var from = new JsonArray(
            new JsonObject { ["id"] = 1, ["value"] = "one" }
        );
        var to = new JsonArray(
            new JsonObject { ["id"] = 1, ["value"] = "one-updated" }
        );
        
        // Act
        var diff = strategy.Diff(from, to);
        
        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Match, 0, 0)
        });
    }

    [Fact]
    public void Diff_WithEmptyArrays_ShouldReturnEmptyDiff()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray();
        var to = new JsonArray();
        
        // Act
        var diff = strategy.Diff(from, to);
        
        // Assert
        diff.ShouldBeEmpty();
    }

    [Fact]
    public void Diff_WithAddToEmptyArray_ShouldReturnAllAdds()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray();
        var to = new JsonArray("A", "B");

        // Act
        var diff = strategy.Diff(from, to);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Add, -1, 0),
            new(LcsDiffEntryType.Add, -1, 1)
        });
    }

    [Fact]
    public void Diff_WithRemoveToEmptyArray_ShouldReturnAllRemoves()
    {
        // Arrange
        var strategy = new ArrayLcsStrategy();
        var from = new JsonArray("A", "B");
        var to = new JsonArray();

        // Act
        var diff = strategy.Diff(from, to);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Remove, 0, -1),
            new(LcsDiffEntryType.Remove, 1, -1)
        });
    }

    #endregion
}