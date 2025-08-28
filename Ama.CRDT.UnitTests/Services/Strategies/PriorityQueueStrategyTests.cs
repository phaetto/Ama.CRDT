namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
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
        
        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }
    
    [Fact]
    public void ApplyOperation_ShouldKeepListSortedByPriority()
    {
        // Arrange
        var model = new TestModel { Items = [new("A", 10), new("C", 30)] };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        var modifiedMeta = metadataManagerA.Initialize(model);

        var patch = patcherA.GeneratePatch(
            document,
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("B", 20), new("C", 30)] }, modifiedMeta));

        // Act
        applicatorA.ApplyPatch(document, patch);

        // Assert
        model.Items.Select(i => i.Id).ShouldBe(new[] { "A", "B", "C" });
        model.Items.Select(i => i.Priority).ShouldBe(new[] { 10, 20, 30 });
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
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("B", 5)] }, metadataManagerA.Initialize(model)));
        
        // Act
        applicatorA.ApplyPatch(document, patch);
        var stateAfterFirst = model.Items.Select(i => i.Id).ToList();
        
        // Clear seen exceptions to test the strategy's own idempotency based on timestamps
        meta.SeenExceptions.Clear();
        applicatorA.ApplyPatch(document, patch);

        // Assert
        model.Items.Select(i => i.Id).ShouldBe(stateAfterFirst);
        model.Items.Select(i => i.Id).ShouldBe(new[] { "B", "A" });
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
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("B", 20)] }, metadataManagerA.Initialize(ancestor)));
        
        var patchC = patcherB.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("C", 5)] }, metadataManagerA.Initialize(ancestor)));
        
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
        var expectedOrder = new[] { "C", "A", "B" };
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
        
        var patcherC = patcherB; // ReplicaId is what matters

        var patch1 = patcherA.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("B", 20)] }, metadataManagerA.Initialize(ancestor)));
        
        var patch2 = patcherB.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("C", 5)] }, metadataManagerA.Initialize(ancestor)));

        var patch3 = patcherC.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("D", 15)] }, metadataManagerA.Initialize(ancestor)));

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
        var expectedOrder = new[] { "C", "A", "D", "B" }; // Priorities: 5, 10, 15, 20
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
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("B", 5)] }, metadataManagerA.Initialize(ancestor)));

        // Replica B changes priority of B to 30 (later)
        Thread.Sleep(5);
        var patchB = patcherB.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 10), new("B", 30)] }, metadataManagerA.Initialize(ancestor)));

        patchA.Operations.ShouldHaveSingleItem();
        patchB.Operations.ShouldHaveSingleItem();

        var opA = patchA.Operations.Single();
        var opB = patchB.Operations.Single();
        
        // Act
        var model = new TestModel { Items = [..ancestor.Items] };
        var meta = metadataManagerA.Initialize(new TestModel { Items = [..ancestor.Items] });
        var document = new CrdtDocument<TestModel>(model, meta);
        applicatorA.ApplyPatch(document, patchA);
        applicatorA.ApplyPatch(document, patchB);

        // Assert
        opB.Timestamp.CompareTo(opA.Timestamp).ShouldBeGreaterThan(0);
        model.Items.Select(i => i.Id).ShouldBe(["A", "B"]);
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
            new CrdtDocument<TestModel>(new TestModel { Items = [] }, metadataManagerA.Initialize(ancestor)));

        // Replica B re-adds A with a new priority (later)
        Thread.Sleep(5);
        var patchAdd = patcherB.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Items = [new("A", 5)] }, metadataManagerA.Initialize(ancestor)));
        
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
    
    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
}