namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

public sealed class FixedSizeArrayStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtFixedSizeArrayStrategy(3)]
        public List<int> Values { get; set; } = new();
    }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;

    public FixedSizeArrayStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, EpochTimestampProvider>()
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
    public void GeneratePatch_ShouldCreateUpsertForChangedElement()
    {
        // Arrange
        var doc1 = new TestModel { Values = [1, 2, 3] };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Values = [1, 99, 3] };
        var meta2 = metadataManagerA.Initialize(doc2);
        
        var crdtDoc1 = new CrdtDocument<TestModel>(doc1, meta1);
        var crdtDoc2 = new CrdtDocument<TestModel>(doc2, meta2);

        // Act
        var patch = patcherA.GeneratePatch(crdtDoc1, crdtDoc2);
        
        // Assert
        patch.Operations.ShouldHaveSingleItem();
        var op = patch.Operations.First();
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.values[1]");
        op.Value.ShouldBe(99);
    }

    [Fact]
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var initialModel = new TestModel { Values = [1, 2, 3] };
        var initialMeta = metadataManagerA.Initialize(initialModel);

        var modifiedModel = new TestModel { Values = [1, 99, 3] };
        var modifiedMeta = metadataManagerA.Initialize(modifiedModel);
        
        Thread.Sleep(5);
        var patch = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(initialModel, initialMeta),
            new CrdtDocument<TestModel>(modifiedModel, modifiedMeta));

        var targetModel = new TestModel { Values = new List<int>(initialModel.Values) };
        var targetMeta = metadataManagerA.Initialize(targetModel);
        var targetDocument = new CrdtDocument<TestModel>(targetModel, targetMeta);

        // Act
        patch.Operations.ShouldHaveSingleItem();
        applicatorA.ApplyPatch(targetDocument, patch);
        var stateAfterFirstApply = new List<int>(targetModel.Values);
        
        applicatorA.ApplyPatch(targetDocument, patch);

        // Assert
        targetModel.Values.ShouldBe(stateAfterFirstApply);
        targetModel.Values.ShouldBe([1, 99, 3]);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentUpdates_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TestModel { Values = [10, 20, 30] };
        var metaAncestor = metadataManagerA.Initialize(ancestor);

        // Replica A updates index 0
        Thread.Sleep(5);
        var metaForA = metadataManagerA.Initialize(new TestModel { Values = [11, 20, 30] });
        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [11, 20, 30] }, metaForA));

        // Replica B updates index 2
        Thread.Sleep(5);
        var metaForB = metadataManagerA.Initialize(new TestModel { Values = [10, 20, 33] });
        var patchB = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [10, 20, 33] }, metaForB));
        
        patchA.Operations.ShouldHaveSingleItem();
        patchB.Operations.ShouldHaveSingleItem();

        // Scenario 1: A then B
        var model1 = new TestModel { Values = new List<int>(ancestor.Values) };
        var meta1 = metadataManagerA.Clone(metaAncestor);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchA);
        applicatorA.ApplyPatch(doc1, patchB);

        // Scenario 2: B then A
        var model2 = new TestModel { Values = new List<int>(ancestor.Values) };
        var meta2 = metadataManagerA.Clone(metaAncestor);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchB);
        applicatorA.ApplyPatch(doc2, patchA);

        // Assert
        var expected = new List<int> { 11, 20, 33 };
        model1.Values.ShouldBe(expected);
        model2.Values.ShouldBe(expected);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentUpdates_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Values = [10, 20, 30] };
        var metaAncestor = metadataManagerA.Initialize(ancestor);

        // Replicas generate patches
        Thread.Sleep(5);
        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [11, 20, 30] }, metaAncestor));
        
        Thread.Sleep(5);
        var patchB = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [10, 22, 30] }, metaAncestor));

        Thread.Sleep(5);
        var patchC = patcherC.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [10, 20, 33] }, metaAncestor));

        var patches = new[] { patchA, patchB, patchC };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<int>>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { Values = new List<int>(ancestor.Values) };
            var meta = metadataManagerA.Clone(metaAncestor);
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in p)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalStates.Add(model.Values);
        }

        // Assert
        var expected = new List<int> { 11, 22, 33 };
        var firstState = finalStates.First();
        firstState.ShouldBe(expected);

        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState);
        }
    }

    [Fact]
    public void Converge_OnConflictingUpdate_LwwShouldWin()
    {
        // Arrange
        var ancestor = new TestModel { Values = [0, 0, 0] };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        
        // Replica A updates index 1
        var metaForA = metadataManagerA.Initialize(new TestModel { Values = [0, 1, 0] });
        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [0, 1, 0] }, metaForA));

        // Replica B updates index 1 later in time
        Thread.Sleep(15); // Ensure timestamp is reliably greater
        var metaForB = metadataManagerA.Initialize(new TestModel { Values = [0, 2, 0] });
        var patchB = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [0, 2, 0] }, metaForB));

        patchA.Operations.ShouldHaveSingleItem();
        patchB.Operations.ShouldHaveSingleItem();

        // Ensure patchB has a higher timestamp
        var opA = patchA.Operations.Single();
        var opB = patchB.Operations.Single();
        var winningOp = opA.Timestamp.CompareTo(opB.Timestamp) > 0 ? opA : opB;
        var winningValue = Convert.ToInt32(winningOp.Value);
        
        // Act
        var model = new TestModel { Values = new List<int>(ancestor.Values) };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        applicatorA.ApplyPatch(document, patchA);
        applicatorA.ApplyPatch(document, patchB);
        
        // Assert
        model.Values[1].ShouldBe(winningValue);
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