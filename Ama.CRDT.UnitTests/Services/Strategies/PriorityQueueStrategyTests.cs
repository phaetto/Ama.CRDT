namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Xunit;

public sealed class PriorityQueueStrategyTests : IDisposable
{
    private sealed class Item(string id, int priority)
    {
        public string Id { get; set; } = id;
        public int Priority { get; set; } = priority;
    }

    private sealed class ItemComparer : IElementComparer
    {
        public bool CanCompare([DisallowNull] Type type)
        {
            return type == typeof(Item);
        }

        public new bool Equals(object? x, object? y) => (x as Item)?.Id == (y as Item)?.Id;
        public int GetHashCode(object obj) => (obj as Item)?.Id?.GetHashCode() ?? 0;
    }

    private sealed class TestModel
    {
        [CrdtPriorityQueueStrategy(nameof(Item.Priority))]
        public List<Item> Items { get; set; } = new();
    }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;

    public PriorityQueueStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtComparer<ItemComparer>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");
        scopeC = scopeFactory.CreateScope("C");
        
        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherC = scopeC.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
        scopeC.Dispose();
    }
    
    [Fact]
    public void ApplyOperation_ShouldKeepListSortedByPriority()
    {
        // Arrange
        var model = new TestModel { Items = [new("A", 10), new("C", 30)] };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);

        var patch = patcherA.GeneratePatch(
            document,
            new TestModel { Items = [new("A", 10), new("B", 20), new("C", 30)] });

        // Act
        applicatorA.ApplyPatch(document, patch);

        // Assert
        model.Items.Select(i => i.Id).ShouldBe(new[] { "C", "B", "A" });
        model.Items.Select(i => i.Priority).ShouldBe(new[] { 30, 20, 10 });
    }
    
    [Fact]
    public void ApplyPatch_IsTrulyIdempotent()
    {
        // Arrange
        var model = new TestModel { Items = [new("A", 10)] };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        
        var patch = patcherA.GeneratePatch(
            document,
            new TestModel { Items = [new("A", 10), new("B", 5)] });
        
        // Act
        applicatorA.ApplyPatch(document, patch);
        var stateAfterFirst = model.Items.Select(i => i.Id).ToList();
        
        // Clear seen exceptions to test the strategy's own idempotency based on timestamps
        meta.SeenExceptions.Clear();
        applicatorA.ApplyPatch(document, patch);

        // Assert
        model.Items.Select(i => i.Id).ShouldBe(stateAfterFirst);
        model.Items.Select(i => i.Id).ShouldBe(new[] { "A", "B" });
    }

    [Fact]
    public void Converge_WhenApplyingConcurrentAdds_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TestModel { Items = [new("A", 10)] };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        
        var patchB = patcherA.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 10), new("B", 20)] });
        
        var patchC = patcherB.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 10), new("C", 5)] });
        
        // Scenario 1: B then C
        var model1 = new TestModel { Items = [..ancestor.Items] };
        var meta1 = metadataManagerA.Initialize(model1);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchB);
        applicatorA.ApplyPatch(doc1, patchC);

        // Scenario 2: C then B
        var model2 = new TestModel { Items = [..ancestor.Items] };
        var meta2 = metadataManagerA.Initialize(model2);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchC);
        applicatorA.ApplyPatch(doc2, patchB);
        
        // Assert
        var expectedOrder = new[] { "B", "A", "C" };
        model1.Items.Select(i => i.Id).ShouldBe(expectedOrder);
        model2.Items.Select(i => i.Id).ShouldBe(expectedOrder);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentAdds_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Items = [new("A", 10)] };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        
        var patch1 = patcherA.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 10), new("B", 20)] });
        
        var patch2 = patcherB.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 10), new("C", 5)] });

        var patch3 = patcherC.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 10), new("D", 15)] });

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<string>>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new TestModel { Items = [..ancestor.Items] };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in permutation)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalStates.Add(model.Items.Select(i => i.Id).ToList());
        }

        // Assert
        var expectedOrder = new[] { "B", "D", "A", "C" }; // Priorities: 20, 15, 10, 5
        var firstState = finalStates.First();
        firstState.ShouldBe(expectedOrder);
        
        foreach(var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState);
        }
    }
    
    [Fact]
    public void Converge_WhenPriorityIsUpdatedConcurrently_LwwWins()
    {
        // Arrange
        var ancestor = new TestModel { Items = [new("A", 10), new("B", 20)] };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        
        // Replica A changes priority of B to 5
        var patchA = patcherA.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 10), new("B", 5)] });

        // Replica B changes priority of B to 30 (later)
        Thread.Sleep(5);
        var patchB = patcherB.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 10), new("B", 30)] });

        patchA.Operations.ShouldHaveSingleItem();
        patchB.Operations.ShouldHaveSingleItem();

        var opA = patchA.Operations.Single();
        var opB = patchB.Operations.Single();
        
        // Act
        var model = new TestModel { Items = [..ancestor.Items] };
        var meta = metaAncestor.DeepClone();
        var document = new CrdtDocument<TestModel>(model, meta);
        applicatorA.ApplyPatch(document, patchA);
        applicatorA.ApplyPatch(document, patchB);

        // Assert
        opB.Timestamp.CompareTo(opA.Timestamp).ShouldBeGreaterThan(0);
        model.Items.Select(i => i.Id).ShouldBe(["B", "A"]);
        model.Items.Single(i => i.Id == "B").Priority.ShouldBe(30);
    }
    
    [Fact]
    public void Converge_WhenItemIsReAddedAfterRemoval_ShouldReappear()
    {
        // Arrange
        var ancestor = new TestModel { Items = [new("A", 10)] };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);

        // Replica A removes A
        var patchRemove = patcherA.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [] });

        // Replica B re-adds A with a new priority (later)
        Thread.Sleep(5);
        var patchAdd = patcherB.GeneratePatch(
            ancestorDocument,
            new TestModel { Items = [new("A", 5)] });
        
        patchRemove.Operations.ShouldHaveSingleItem();
        patchAdd.Operations.ShouldHaveSingleItem();

        var opRemove = patchRemove.Operations.Single();
        var opAdd = patchAdd.Operations.Single();
        var addWins = opAdd.Timestamp.CompareTo(opRemove.Timestamp) > 0;
        addWins.ShouldBeTrue();

        // Act
        var model = new TestModel { Items = [..ancestor.Items] };
        var document = new CrdtDocument<TestModel>(model, metaAncestor);
        applicatorA.ApplyPatch(document, patchRemove);
        applicatorA.ApplyPatch(document, patchAdd);
        
        // Assert
        model.Items.ShouldHaveSingleItem();
        model.Items[0].Priority.ShouldBe(5);
    }

    [Fact]
    public void GenerateOperation_WithAddIntent_ShouldGenerateUpsertOperation()
    {
        // Arrange
        var model = new TestModel { Items = [] };
        var meta = metadataManagerA.Initialize(model);
        var itemToAdd = new Item("A", 10);
        
        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<PriorityQueueStrategy>().Single();
        var timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        
        var context = new GenerateOperationContext(
            model,
            meta,
            nameof(TestModel.Items),
            typeof(TestModel).GetProperty(nameof(TestModel.Items))!,
            new AddIntent(itemToAdd),
            timestampProvider.Now(),
            0);

        // Act
        var op = strategy.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBeOfType<Item>().Id.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithRemoveValueIntent_ShouldGenerateRemoveOperation()
    {
        // Arrange
        var itemToRemove = new Item("A", 10);
        var model = new TestModel { Items = [itemToRemove] };
        var meta = metadataManagerA.Initialize(model);
        
        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<PriorityQueueStrategy>().Single();
        var timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        
        var context = new GenerateOperationContext(
            model,
            meta,
            nameof(TestModel.Items),
            typeof(TestModel).GetProperty(nameof(TestModel.Items))!,
            new RemoveValueIntent(itemToRemove),
            timestampProvider.Now(),
            0);

        // Act
        var op = strategy.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBeOfType<Item>().Id.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var model = new TestModel { Items = [] };
        var meta = metadataManagerA.Initialize(model);
        
        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<PriorityQueueStrategy>().Single();
        var timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        
        var context = new GenerateOperationContext(
            model,
            meta,
            nameof(TestModel.Items),
            typeof(TestModel).GetProperty(nameof(TestModel.Items))!,
            new IncrementIntent(1),
            timestampProvider.Now(),
            0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }

    [Fact]
    public void Compact_ShouldRemoveDeadItems_WhenPolicyAllows()
    {
        // Arrange
        var doc = new TestModel { Items = new List<Item> { new Item("alive", 1) } };
        var meta = new CrdtMetadata();
        
        var tsProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var safeTs1 = tsProvider.Create(10);
        var safeTs2 = tsProvider.Create(20);
        var unsafeTs = tsProvider.Create(30);
        
        var comparer = new ItemComparer();
        var adds = new Dictionary<object, ICrdtTimestamp>(comparer);
        var removes = new Dictionary<object, ICrdtTimestamp>(comparer);

        // Alive: addTs > removeTs
        var itemAlive = new Item("alive", 1);
        adds[itemAlive] = safeTs2;
        removes[itemAlive] = safeTs1;

        // Dead Safe: addTs <= removeTs
        var itemDeadSafe = new Item("dead_safe", 2);
        adds[itemDeadSafe] = safeTs1;
        removes[itemDeadSafe] = safeTs2;

        // Dead Unsafe
        var itemDeadUnsafe = new Item("dead_unsafe", 3);
        adds[itemDeadUnsafe] = safeTs1;
        removes[itemDeadUnsafe] = unsafeTs;

        // Dead No Add Safe
        var itemDeadNoAdd = new Item("dead_no_add", 4);
        removes[itemDeadNoAdd] = safeTs1;

        meta.PriorityQueues["$.Items"] = new LwwSetState(adds, removes);

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(safeTs1)).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(safeTs2)).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(unsafeTs)).Returns(false);

        var context = new CompactionContext(meta, mockPolicy.Object, "Items", "$.Items", doc);
        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<PriorityQueueStrategy>().Single();

        // Act
        strategy.Compact(context);

        // Assert
        var queueState = meta.PriorityQueues["$.Items"];
        
        queueState.Adds.ShouldContainKey(itemAlive);
        queueState.Removes.ShouldContainKey(itemAlive);

        queueState.Adds.ShouldNotContainKey(itemDeadSafe);
        queueState.Removes.ShouldNotContainKey(itemDeadSafe);

        queueState.Adds.ShouldContainKey(itemDeadUnsafe);
        queueState.Removes.ShouldContainKey(itemDeadUnsafe);

        queueState.Removes.ShouldNotContainKey(itemDeadNoAdd);
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