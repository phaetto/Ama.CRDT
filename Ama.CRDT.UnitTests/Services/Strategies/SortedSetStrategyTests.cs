namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.ShowCase.Models;
using Microsoft.Extensions.Options;
using Ama.CRDT.Attributes;
using Ama.CRDT.ShowCase.Services;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using static Ama.CRDT.Services.Strategies.SortedSetStrategy;

public sealed class SortedSetStrategyTests
{
    private sealed class TestModel
    {
        public List<NestedModel>? Items { get; init; }
    }
    
    private sealed class MutableTestModel
    {
        public List<string> Items { get; set; } = new();
    }

    private sealed record NestedModel
    {
        public int Id { get; init; }
        public string? Value { get; init; }
    }
    
    private sealed class NestedModelIdComparer : IElementComparer
    {
        public bool CanCompare(Type type) => type == typeof(NestedModel);

        public new bool Equals(object? x, object? y)
        {
            if (x is not NestedModel modelX || y is not NestedModel modelY)
            {
                return object.Equals(x, y);
            }

            return modelX.Id == modelY.Id;
        }

        public int GetHashCode(object obj)
        {
            if (obj is NestedModel model)
            {
                return model.Id.GetHashCode();
            }
            return obj.GetHashCode();
        }
    }

    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly Mock<IElementComparerProvider> mockComparerProvider = new();
    private readonly Mock<ICrdtTimestampProvider> mockTimestampProvider = new();
    private readonly SortedSetStrategy strategy;

    public SortedSetStrategyTests()
    {
        strategy = new SortedSetStrategy(mockComparerProvider.Object, mockTimestampProvider.Object, Options.Create(new CrdtOptions { ReplicaId = "test-array-strategy" }));
        mockComparerProvider
            .Setup(p => p.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);
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
        
        var originalValue = new List<NestedModel>
        {
            new() { Id = 1, Value = "one" },
            new() { Id = 2, Value = "two" }
        };
        var modifiedValue = new List<NestedModel>
        {
            new() { Id = 1, Value = "one" },
            new() { Id = 2, Value = "two-updated" }
        };

        mockPatcher
            .Setup(p => p.DifferentiateObject(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<object>(), It.IsAny<CrdtMetadata>(), It.IsAny<object>(), It.IsAny<CrdtMetadata>(), It.IsAny<List<CrdtOperation>>(), It.IsAny<object>(), It.IsAny<object>()))
            .Callback<string, Type, object, CrdtMetadata, object, CrdtMetadata, List<CrdtOperation>, object, object>((itemPath, _, from, _, to, _, ops, _, _) =>
            {
                var toNested = (NestedModel)to;
                ops.Add(new CrdtOperation(Guid.NewGuid(), "mock-replica", $"{itemPath}.value", OperationType.Upsert, toNested.Value, new EpochTimestamp(0)));
            });
        
        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, path, property, originalValue, modifiedValue, new object(), new object(), new CrdtMetadata(), new CrdtMetadata());

        // Assert
        mockPatcher.Verify(p => p.DifferentiateObject(
            "$.items[1]",
            typeof(NestedModel),
            It.Is<NestedModel>(o => o.Id == 2),
            It.IsAny<CrdtMetadata>(),
            It.Is<NestedModel>(o => o.Id == 2 && o.Value == "two-updated"),
            It.IsAny<CrdtMetadata>(),
            It.IsAny<List<CrdtOperation>>(),
            It.IsAny<object>(),
            It.IsAny<object>()
        ), Times.Once);
        
        operations.ShouldHaveSingleItem();
        operations.Single().JsonPath.ShouldBe("$.items[1].value");
    }
    
    [Fact]
    public void ApplyOperation_Upsert_ShouldInsertItemIntoArrayAndSort()
    {
        // Arrange
        var model = new MutableTestModel { Items = { "a", "c" } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.items[1]", OperationType.Upsert, "b", new EpochTimestamp(1L));
        mockComparerProvider.Setup(p => p.GetComparer(typeof(string))).Returns(EqualityComparer<object>.Default);

        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);

        // Assert
        var list = model.Items;
        list.Count.ShouldBe(3);
        list[0].ShouldBe("a");
        list[1].ShouldBe("b");
        list[2].ShouldBe("c");
    }

    [Fact]
    public void ApplyOperation_Remove_ShouldRemoveItemFromArray()
    {
        // Arrange
        var model = new MutableTestModel { Items = { "a", "b", "c" } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.items[1]", OperationType.Remove, "b", new EpochTimestamp(1L));
        mockComparerProvider.Setup(p => p.GetComparer(typeof(string))).Returns(EqualityComparer<object>.Default);

        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);

        // Assert
        var list = model.Items;
        list.Count.ShouldBe(2);
        list[0].ShouldBe("a");
        list[1].ShouldBe("c");
    }
    
    #region Diff Method Tests

    [Fact]
    public void Diff_WhenArraysAreIdentical_ShouldReturnAllMatches()
    {
        // Arrange
        var from = new List<object> { 1, 2, 3 };
        var to = new List<object> { 1, 2, 3 };

        // Act
        var diff = strategy.Diff(from, to, EqualityComparer<object>.Default);

        // Assert
        diff.ShouldBe(new List<LcsDiffEntry>
        {
            new(LcsDiffEntryType.Match, 0, 0),
            new(LcsDiffEntryType.Match, 1, 1),
            new(LcsDiffEntryType.Match, 2, 2)
        });
    }
    
    #endregion

    private sealed record ConvergenceTestModel
    {
        [CrdtSortedSetStrategy]
        public List<User> Users { get; init; } = new();
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentArrayInsertions_ShouldBeCommutativeAndConverge()
    {
        // Arrange
        var userA = new User(Guid.NewGuid(), "Alice");
        var userB = new User(Guid.NewGuid(), "Bob");

        var (patcherA, applicator) = CreateCrdtServices("replica-A");
        var (patcherB, _) = CreateCrdtServices("replica-B");
    
        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel(), new CrdtMetadata()), 
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userA] }, new CrdtMetadata()));

        var patchB = patcherB.GeneratePatch(
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel(), new CrdtMetadata()), 
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userB] }, new CrdtMetadata()));

        // Scenario 1: Apply Patch A, then Patch B
        var modelAb = new ConvergenceTestModel();
        var metadataAb = new CrdtMetadata();
        applicator.ApplyPatch(modelAb, patchA, metadataAb);
        applicator.ApplyPatch(modelAb, patchB, metadataAb);

        // Scenario 2: Apply Patch B, then Patch A
        var modelBa = new ConvergenceTestModel();
        var metadataBa = new CrdtMetadata();
        applicator.ApplyPatch(modelBa, patchB, metadataBa);
        applicator.ApplyPatch(modelBa, patchA, metadataBa);
    
        // Assert
        JsonSerializer.Serialize(modelAb).ShouldBe(JsonSerializer.Serialize(modelBa));
        
        modelAb.Users.Count.ShouldBe(2);
        modelBa.Users.Count.ShouldBe(2);
        modelAb.Users.ShouldContain(u => u.Id == userA.Id);
        modelAb.Users.ShouldContain(u => u.Id == userB.Id);
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var userA = new User(Guid.NewGuid(), "Alice");
        var (patcher, applicator) = CreateCrdtServices("replica-A");
        var patch = patcher.GeneratePatch(
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel(), new CrdtMetadata()),
            new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userA] }, new CrdtMetadata()));
    
        var model = new ConvergenceTestModel();
        var metadata = new CrdtMetadata();

        // Act
        applicator.ApplyPatch(model, patch, metadata);
        var stateAfterFirst = JsonSerializer.Serialize(model);
        applicator.ApplyPatch(model, patch, metadata);
        var stateAfterSecond = JsonSerializer.Serialize(model);

        // Assert
        stateAfterSecond.ShouldBe(stateAfterFirst);
        model.Users.Count.ShouldBe(1);
        model.Users.ShouldContain(u => u.Id == userA.Id);
    }

    [Fact]
    public void ApplyPatch_WithConcurrentArrayInsertions_ShouldBeCommutativeAndAssociativeAndConverge()
    {
        // Arrange
        var userA = new User(Guid.NewGuid(), "Alice");
        var userB = new User(Guid.NewGuid(), "Bob");
        var userC = new User(Guid.NewGuid(), "Charlie");

        var (patcherA, applicator) = CreateCrdtServices("replica-A");
        var (patcherB, _) = CreateCrdtServices("replica-B");
        var (patcherC, _) = CreateCrdtServices("replica-C");

        var initialDoc = new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel(), new CrdtMetadata());
        var emptyMeta = new CrdtMetadata();

        var patchA = patcherA.GeneratePatch(initialDoc, new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userA] }, emptyMeta));
        var patchB = patcherB.GeneratePatch(initialDoc, new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userB] }, emptyMeta));
        var patchC = patcherC.GeneratePatch(initialDoc, new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel { Users = [userC] }, emptyMeta));

        var patches = new[] { patchA, patchB, patchC };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<string>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new ConvergenceTestModel();
            var meta = new CrdtMetadata();
            foreach (var patch in permutation)
            {
                applicator.ApplyPatch(model, patch, meta);
            }
            finalStates.Add(JsonSerializer.Serialize(model));
        }

        // Assert
        var firstState = finalStates.First();
        var firstModel = JsonSerializer.Deserialize<ConvergenceTestModel>(firstState);
        firstModel!.Users.Count.ShouldBe(3);
    
        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState);
        }
    }

    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
    
    private (ICrdtPatcher patcher, ICrdtApplicator applicator) CreateCrdtServices(string replicaId)
    {
        var options = Options.Create(new CrdtOptions { ReplicaId = replicaId });
        var timestampProvider = new EpochTimestampProvider();

        var userComparer = new CaseInsensitiveStringComparer();
        var comparerProvider = new ElementComparerProvider([userComparer]);

        var lwwStrategy = new LwwStrategy(options);
        var counterStrategy = new CounterStrategy(timestampProvider, options);
        var arrayLcsStrategy = new SortedSetStrategy(comparerProvider, timestampProvider, options);
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayLcsStrategy };
        
        var strategyManager = new CrdtStrategyManager(strategies);
        
        var patcher = new CrdtPatcher(strategyManager);
        var applicator = new CrdtApplicator(strategyManager);
        
        return (patcher, applicator);
    }
}