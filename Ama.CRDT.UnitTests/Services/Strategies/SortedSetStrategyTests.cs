namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.ShowCase.Models;
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
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Extensions;

public sealed class SortedSetStrategyTests : IDisposable
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

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly ICrdtMetadataManager metadataManagerB;
    private readonly ICrdtTimestampProvider timestampProvider;

    public SortedSetStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtComparer<CaseInsensitiveStringComparer>()
            .AddSingleton<ICrdtTimestampProvider, EpochTimestampProvider>()
            .BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scopeA = scopeFactory.CreateScope("replica-A");
        scopeB = scopeFactory.CreateScope("replica-B");
        scopeC = scopeFactory.CreateScope("replica-C");
        
        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherC = scopeC.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        metadataManagerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = serviceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
        scopeC.Dispose();
    }

    [Fact]
    public void GeneratePatch_WithCustomIdComparer_WhenObjectInArrayIsModified_ShouldCallPatcherDifferentiateObject()
    {
        // Arrange
        var mockPatcher = new Mock<ICrdtPatcher>();
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        var mockTimestampProvider = new Mock<ICrdtTimestampProvider>();
        var strategy = new SortedSetStrategy(mockComparerProvider.Object, new ReplicaContext { ReplicaId = "replica-A" });

        mockComparerProvider
            .Setup(p => p.GetComparer(typeof(NestedModel)))
            .Returns(new NestedModelIdComparer());
        mockTimestampProvider.Setup(p => p.Create(It.IsAny<long>())).Returns<long>(val => new SequentialTimestamp(val));

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
            .Setup(p => p.DifferentiateObject(It.IsAny<DifferentiateObjectContext>()))
            .Callback<DifferentiateObjectContext>(ctx =>
            {
                var (itemPath, _, _, to, _, _, _, ops, _) = ctx;
                var toNested = (NestedModel)to!;
                ops.Add(new CrdtOperation(Guid.NewGuid(), "mock-replica", $"{itemPath}.value", OperationType.Upsert, toNested.Value, mockTimestampProvider.Object.Create(0)));
            });
        
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, path, property, originalValue, modifiedValue, new object(), new object(), new CrdtMetadata(), mockTimestampProvider.Object.Create(0));
        
        // Act
        strategy.GeneratePatch(context);

        // Assert
        mockPatcher.Verify(p => p.DifferentiateObject(
            It.Is<DifferentiateObjectContext>(ctx => 
                ctx.Path == "$.items[1]" &&
                ctx.Type == typeof(NestedModel)
            )
        ), Times.Once);
        
        operations.ShouldHaveSingleItem();
        operations.Single().JsonPath.ShouldBe("$.items[1].value");
    }
    
    [Fact]
    public void ApplyOperation_Upsert_ShouldInsertItemIntoArrayAndSort()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var model = new MutableTestModel { Items = { "a", "c" } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.items[1]", OperationType.Upsert, "b", timestampProvider.Create(1L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act
        strategy.ApplyOperation(context);

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
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var model = new MutableTestModel { Items = { "a", "b", "c" } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.items[1]", OperationType.Remove, "b", timestampProvider.Create(1L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act
        strategy.ApplyOperation(context);

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
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
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

        var modelA = new ConvergenceTestModel { Users = [userA] };
        var modelB = new ConvergenceTestModel { Users = [userB] };

        var doc0 = new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel(), metadataManagerA.Initialize(new ConvergenceTestModel()));
        
        var patchA = patcherA.GeneratePatch(doc0, modelA);
        var patchB = patcherB.GeneratePatch(doc0, modelB);

        // Scenario 1: Apply Patch A, then Patch B
        var modelAb = new ConvergenceTestModel();
        var metadataAb = metadataManagerA.Initialize(modelAb);
        var docAb = new CrdtDocument<ConvergenceTestModel>(modelAb, metadataAb);
        applicatorA.ApplyPatch(docAb, patchA);
        applicatorA.ApplyPatch(docAb, patchB);

        // Scenario 2: Apply Patch B, then Patch A
        var modelBa = new ConvergenceTestModel();
        var metadataBa = metadataManagerA.Initialize(modelBa);
        var docBa = new CrdtDocument<ConvergenceTestModel>(modelBa, metadataBa);
        applicatorA.ApplyPatch(docBa, patchB);
        applicatorA.ApplyPatch(docBa, patchA);
    
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

        var modelA = new ConvergenceTestModel { Users = [userA] };

        var doc0 = new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel(), metadataManagerA.Initialize(new ConvergenceTestModel()));
        
        var patch = patcherA.GeneratePatch(doc0, modelA);
    
        var model = new ConvergenceTestModel();
        var metadata = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<ConvergenceTestModel>(model, metadata);

        // Act
        applicatorA.ApplyPatch(document, patch);
        var stateAfterFirst = JsonSerializer.Serialize(model);
        applicatorA.ApplyPatch(document, patch);
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

        var modelA = new ConvergenceTestModel { Users = [userA] };
        var modelB = new ConvergenceTestModel { Users = [userB] };
        var modelC = new ConvergenceTestModel { Users = [userC] };

        var initialDoc = new CrdtDocument<ConvergenceTestModel>(new ConvergenceTestModel(), metadataManagerA.Initialize(new ConvergenceTestModel()));
        
        var patchA = patcherA.GeneratePatch(initialDoc, modelA);
        var patchB = patcherB.GeneratePatch(initialDoc, modelB);
        var patchC = patcherC.GeneratePatch(initialDoc, modelC);

        var patches = new[] { patchA, patchB, patchC };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<string>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new ConvergenceTestModel();
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<ConvergenceTestModel>(model, meta);
            foreach (var patch in permutation)
            {
                applicatorA.ApplyPatch(document, patch);
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
}