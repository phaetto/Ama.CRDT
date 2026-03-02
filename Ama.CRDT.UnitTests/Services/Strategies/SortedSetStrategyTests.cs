namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Attributes;
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
        mockTimestampProvider.Setup(p => p.Create(It.IsAny<long>())).Returns<long>(val => new EpochTimestamp(val));

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

    #region Intents Generation Tests

    [Fact]
    public void GenerateOperation_AddIntent_ShouldReturnUpsertOperation()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var doc = new ConvergenceTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var property = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;
        var timestamp = timestampProvider.Now();
        var intent = new AddIntent(new TestUser("Eve", "Eve"));
        
        var context = new GenerateOperationContext(doc, meta, "$.users", property, intent, timestamp, "r1");

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.users[-1]");
        operation.Timestamp.ShouldBe(timestamp);
        operation.ReplicaId.ShouldBe("r1");
        
        var user = operation.Value.ShouldBeOfType<TestUser>();
        user.Name.ShouldBe("Eve");
    }

    [Fact]
    public void GenerateOperation_RemoveValueIntent_ShouldReturnRemoveOperation()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var doc = new ConvergenceTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var property = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;
        var timestamp = timestampProvider.Now();
        var intent = new RemoveValueIntent(new TestUser("Bob", "Bob"));
        
        var context = new GenerateOperationContext(doc, meta, "$.users", property, intent, timestamp, "r1");

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Remove);
        operation.JsonPath.ShouldBe("$.users[-1]");
        operation.Timestamp.ShouldBe(timestamp);
        operation.ReplicaId.ShouldBe("r1");
        
        var user = operation.Value.ShouldBeOfType<TestUser>();
        user.Id.ShouldBe("Bob");
    }

    [Fact]
    public void GenerateOperation_UnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var doc = new ConvergenceTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var property = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;
        var timestamp = timestampProvider.Now();
        var intent = new SetIntent("Unsupported");
        
        var context = new GenerateOperationContext(doc, meta, "$.users", property, intent, timestamp, "r1");

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }
    
    [Fact]
    public void ApplyOperation_FromGeneratedIntent_ShouldModifyCollection()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var doc = new ConvergenceTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var property = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;
        
        // Generate an add operation using explicit intent
        var addContext = new GenerateOperationContext(doc, meta, "$.users", property, new AddIntent(new TestUser("Frank", "Frank")), timestampProvider.Now(), "r1");
        var addOperation = strategy.GenerateOperation(addContext);
        
        // Apply the operation
        var applyContext = new ApplyOperationContext(doc, meta, addOperation);
        strategy.ApplyOperation(applyContext);

        doc.Users.Count.ShouldBe(1);
        doc.Users[0].Name.ShouldBe("Frank");
        
        // Generate a remove operation using explicit intent
        var removeContext = new GenerateOperationContext(doc, meta, "$.users", property, new RemoveValueIntent(new TestUser("Frank", "Frank")), timestampProvider.Now(), "r1");
        var removeOperation = strategy.GenerateOperation(removeContext);
        
        // Apply the remove operation
        var removeApplyContext = new ApplyOperationContext(doc, meta, removeOperation);
        strategy.ApplyOperation(removeApplyContext);

        doc.Users.ShouldBeEmpty();
    }

    #endregion
    
    [Fact]
    public void ApplyOperation_Upsert_ShouldInsertItemIntoArrayAndSort()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var model = new MutableTestModel { Items = { "a", "c" } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Items[1]", OperationType.Upsert, "b", timestampProvider.Create(1L));
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

    private sealed record SortTestModel
    {
        [CrdtSortedSetStrategy(nameof(Item.Name))]
        public List<Item> Items { get; set; } = new();
    }

    private sealed record Item(int Id, string Name);
    
    [Fact]
    public void ApplyOperation_Upsert_WithSortPropertyName_ShouldInsertAndSortByName()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var model = new SortTestModel { Items = { new Item(1, "c"), new Item(2, "a") } };
        
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Items[2]", OperationType.Upsert, new Item(3, "b"), timestampProvider.Create(1L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act
        strategy.ApplyOperation(context);

        // Assert
        var list = model.Items;
        list.Count.ShouldBe(3);
        list[0].Name.ShouldBe("a");
        list[1].Name.ShouldBe("b");
        list[2].Name.ShouldBe("c");
    }

    [Fact]
    public void ApplyOperation_Remove_ShouldRemoveItemFromArray()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var model = new MutableTestModel { Items = { "a", "b", "c" } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Items[1]", OperationType.Remove, "b", timestampProvider.Create(1L));
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

    private sealed record TestUser(string Id, string Name);

    private sealed record ConvergenceTestModel
    {
        [CrdtSortedSetStrategy] // Defaults to "Id"
        public List<TestUser> Users { get; init; } = new();
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentArrayInsertions_ShouldBeCommutativeAndConverge()
    {
        // Arrange
        var userA = new TestUser("Alice", "Alice");
        var userB = new TestUser("Bob", "Bob");

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
        modelAb.Users.Select(u => u.Name).ShouldBe(new[] { "Alice", "Bob" });
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var userA = new TestUser("Alice", "Alice");

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
        var userA = new TestUser("Alice", "Alice");
        var userB = new TestUser("Bob", "Bob");
        var userC = new TestUser("Charlie", "Charlie");

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

    [Fact]
    public void GetStartKey_ShouldReturnSmallestKeyOrNull()
    {
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var propInfo = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;
        
        strategy.GetStartKey(new ConvergenceTestModel(), propInfo).ShouldBeNull();
        strategy.GetStartKey(new ConvergenceTestModel { Users = { new TestUser("Charlie", "Charlie"), new TestUser("Alice", "Alice") } }, propInfo).ShouldBe("Alice");
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractCorrectly()
    {
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var op = new CrdtOperation(Guid.NewGuid(), "r1", "$.users", OperationType.Upsert, new TestUser("Bob", "Bob"), timestampProvider.Now());
        
        strategy.GetKeyFromOperation(op, "$.users").ShouldBe("Bob");
        strategy.GetKeyFromOperation(op, "$.otherPath").ShouldBeNull();
    }

    [Fact]
    public void GetMinimumKey_ShouldReturnCorrectMinValue()
    {
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var propInfo = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;
        strategy.GetMinimumKey(propInfo).ShouldBe(string.Empty);
    }

    [Fact]
    public void Split_ShouldDivideDataEquallyAndMaintainSort()
    {
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var doc = new ConvergenceTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var propInfo = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;

        strategy.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[0]", OperationType.Upsert, new TestUser("Alice", "Alice"), timestampProvider.Now())));
        strategy.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[1]", OperationType.Upsert, new TestUser("Bob", "Bob"), timestampProvider.Now())));
        strategy.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[2]", OperationType.Upsert, new TestUser("Charlie", "Charlie"), timestampProvider.Now())));
        strategy.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[3]", OperationType.Upsert, new TestUser("Dave", "Dave"), timestampProvider.Now())));

        var result = strategy.Split(doc, meta, propInfo);

        result.SplitKey.ShouldBe("Charlie");

        var doc1 = (ConvergenceTestModel)result.Partition1.Data;
        var doc2 = (ConvergenceTestModel)result.Partition2.Data;

        doc1.Users.Select(u => u.Name).ShouldBe(["Alice", "Bob"], ignoreOrder: true);
        doc2.Users.Select(u => u.Name).ShouldBe(["Charlie", "Dave"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_ShouldCombineDataAndSort()
    {
        var strategy = scopeA.ServiceProvider.GetRequiredService<SortedSetStrategy>();
        var doc1 = new ConvergenceTestModel();
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new ConvergenceTestModel();
        var meta2 = metadataManagerA.Initialize(doc2);
        var propInfo = typeof(ConvergenceTestModel).GetProperty(nameof(ConvergenceTestModel.Users))!;

        strategy.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[0]", OperationType.Upsert, new TestUser("Dave", "Dave"), timestampProvider.Now())));
        strategy.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[1]", OperationType.Upsert, new TestUser("Alice", "Alice"), timestampProvider.Now())));
        
        strategy.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[0]", OperationType.Upsert, new TestUser("Charlie", "Charlie"), timestampProvider.Now())));
        strategy.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.users[1]", OperationType.Upsert, new TestUser("Bob", "Bob"), timestampProvider.Now())));

        var result = strategy.Merge(doc1, meta1, doc2, meta2, propInfo);

        var mergedDoc = (ConvergenceTestModel)result.Data;
        mergedDoc.Users.Select(u => u.Name).ShouldBe(["Alice", "Bob", "Charlie", "Dave"]); // Already sorted correctly by Name
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