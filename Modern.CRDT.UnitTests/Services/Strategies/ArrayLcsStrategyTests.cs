namespace Modern.CRDT.UnitTests.Services.Strategies;

using Microsoft.Extensions.Options;
using Modern.CRDT.Attributes;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Modern.CRDT.ShowCase.Models;
using Modern.CRDT.ShowCase.Services;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    
    private sealed record StringListModel
    {
        public List<string> Items { get; init; } = new();
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
    private readonly Mock<ICrdtTimestampProvider> mockTimestampProvider = new();
    private readonly ArrayLcsStrategy strategy;

    public ArrayLcsStrategyTests()
    {
        strategy = new ArrayLcsStrategy(mockComparerProvider.Object, mockTimestampProvider.Object, Options.Create(new CrdtOptions { ReplicaId = "test-array-strategy" }));
        mockComparerProvider
            .Setup(p => p.GetComparer(It.IsAny<Type>()))
            .Returns(JsonNodeDeepEqualityComparer.Instance);
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
            .Setup(p => p.DifferentiateObject(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<JsonObject>(), It.IsAny<List<CrdtOperation>>()))
            .Callback<string, Type, JsonObject, JsonObject, JsonObject, JsonObject, List<CrdtOperation>>((itemPath, _, _, _, toObject, _, ops) =>
            {
                ops.Add(new CrdtOperation(Guid.NewGuid(), "mock-replica", $"{itemPath}.value", OperationType.Upsert, toObject["value"]!.DeepClone(), new EpochTimestamp(0)));
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
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.items[1]", OperationType.Upsert, JsonValue.Create("b"), new EpochTimestamp(1L));
        var property = typeof(StringListModel).GetProperty(nameof(StringListModel.Items))!;
        mockComparerProvider.Setup(p => p.GetComparer(typeof(string))).Returns(JsonNodeDeepEqualityComparer.Instance);

        // Act
        strategy.ApplyOperation(rootNode, operation, property);

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
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.items[1]", OperationType.Remove, JsonValue.Create("b"), new EpochTimestamp(1L));
        var property = typeof(StringListModel).GetProperty(nameof(StringListModel.Items))!;
        mockComparerProvider.Setup(p => p.GetComparer(typeof(string))).Returns(JsonNodeDeepEqualityComparer.Instance);

        // Act
        strategy.ApplyOperation(rootNode, operation, property);

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

    private sealed record ConvergenceTestModel
    {
        [CrdtArrayLcsStrategy]
        public List<User> Users { get; init; } = new();
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentArrayInsertions_ShouldBeCommutativeAndConverge()
    {
        // NOTE: This test is expected to FAIL with the current implementation.
        // It demonstrates a convergence bug where applying concurrent "add" operations
        // in different orders results in different final states for an array.
        // The root cause is that `JsonArray.Insert(index, ...)` is not a commutative operation.

        // Arrange
        var userA = new User(Guid.NewGuid(), "Alice");
        var userB = new User(Guid.NewGuid(), "Bob");

        // We need two sets of services to simulate two different replicas.
        // The applicator can be shared as it's stateless.
        var (patcherA, applicator) = CreateCrdtServices("replica-A");
        var (patcherB, _) = CreateCrdtServices("replica-B");
    
        // --- Generate Patches ---

        // Replica A generates a patch to add User A to an empty list.
        // This results in an operation: Upsert at path '$.users[0]'.
        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel()), 
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userA] }));

        // Replica B generates a patch to add User B to an empty list.
        // This also results in an operation: Upsert at path '$.users[0]'.
        var patchB = patcherB.GeneratePatch(
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel()), 
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userB] }));

        patchA.Operations.ShouldHaveSingleItem().JsonPath.ShouldBe("$.users[0]");
        patchB.Operations.ShouldHaveSingleItem().JsonPath.ShouldBe("$.users[0]");

        // --- Simulation ---

        // Scenario 1: Apply Patch A, then Patch B
        var metadataAb = new CrdtMetadata();
        var modelAbAfterA = applicator.ApplyPatch(new ConvergenceTestModel(), patchA, metadataAb);
        var finalModelAb = applicator.ApplyPatch(modelAbAfterA, patchB, metadataAb);

        // Scenario 2: Apply Patch B, then Patch A
        var metadataBa = new CrdtMetadata();
        var modelBaAfterB = applicator.ApplyPatch(new ConvergenceTestModel(), patchB, metadataBa);
        var finalModelBa = applicator.ApplyPatch(modelBaAfterB, patchA, metadataBa);
    
        // --- Assert ---

        // Verify that both scenarios produced the same final state.
        // The current implementation will fail here because:
        // Scenario 1 (A then B):
        // 1. Apply A: Users = [userA]
        // 2. Apply B (insert at index 0): Users = [userB, userA]
        //
        // Scenario 2 (B then A):
        // 1. Apply B: Users = [userB]
        // 2. Apply A (insert at index 0): Users = [userA, userB]
        // The final lists are not equal.
    
        // To make this test pass, the array strategy would need a commutative way
        // of handling insertions, e.g., by always sorting the array by a stable key
        // after an operation.
    
        JsonSerializer.Serialize(finalModelAb).ShouldBe(JsonSerializer.Serialize(finalModelBa));
        
        // Secondary assertions to confirm the content is correct, even if order is not.
        finalModelAb.Users.Count.ShouldBe(2);
        finalModelBa.Users.Count.ShouldBe(2);
        finalModelAb.Users.ShouldContain(userA);
        finalModelAb.Users.ShouldContain(userB);
        finalModelBa.Users.ShouldContain(userA);
        finalModelBa.Users.ShouldContain(userB);
    }
    
    private (IJsonCrdtPatcher patcher, IJsonCrdtApplicator applicator) CreateCrdtServices(string replicaId)
    {
        var options = Options.Create(new CrdtOptions { ReplicaId = replicaId });
        var timestampProvider = new EpochTimestampProvider();

        var userComparer = new UserByIdComparer();
        var comparerProvider = new JsonNodeComparerProvider([userComparer]);

        var lwwStrategy = new LwwStrategy(options);
        var counterStrategy = new CounterStrategy(timestampProvider, options);
        var arrayLcsStrategy = new ArrayLcsStrategy(comparerProvider, timestampProvider, options);
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayLcsStrategy };
        
        var strategyManager = new CrdtStrategyManager(strategies);
        
        var patcher = new JsonCrdtPatcher(strategyManager);
        var applicator = new JsonCrdtApplicator(strategyManager);
        
        return (patcher, applicator);
    }
}