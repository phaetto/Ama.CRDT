namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class TwoPhaseSetStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtTwoPhaseSetStrategy]
        public List<string> Tags { get; set; } = new();
    }
    
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;

    public TwoPhaseSetStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
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
    public void GeneratePatch_ShouldCreateUpsertAndRemoveOps()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "B", "C" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), doc2);
        
        // Assert
        patch.Operations.Count.ShouldBe(2);
        patch.Operations.ShouldContain(op => op.Type == OperationType.Remove && op.Value.ToString() == "A");
        patch.Operations.ShouldContain(op => op.Type == OperationType.Upsert && op.Value.ToString() == "C");
    }
    
    [Fact]
    public void ApplyPatch_IsTrulyIdempotent()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A", "B" } };
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), doc2);

        var target = new TestModel { Tags = { "A" } };
        var targetMeta = metadataManagerA.Initialize(target);
        var targetDocument = new CrdtDocument<TestModel>(target, targetMeta);
        
        // Act
        applicatorA.ApplyPatch(targetDocument, patch);
        var stateAfterFirst = new List<string>(target.Tags);
        
        // Clear seen exceptions to test the strategy's own idempotency
        targetMeta.SeenExceptions.Clear();
        applicatorA.ApplyPatch(targetDocument, patch);
        
        // Assert
        target.Tags.ShouldBe(stateAfterFirst);
        target.Tags.ShouldBe(new[] { "A", "B" }, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_WhenItemIsRemoved_ItCannotBeReAdded()
    {
        // Arrange
        var model = new TestModel { Tags = { "A" } };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        
        // Remove "A"
        var patchRemove = patcherA.GeneratePatch(document, new TestModel());
        applicatorA.ApplyPatch(document, patchRemove);
        model.Tags.ShouldBeEmpty();
        
        // Try to add "A" back
        var patchAdd = patcherA.GeneratePatch(new CrdtDocument<TestModel>(new TestModel(), meta), new TestModel { Tags = { "A" } });
        
        // Act
        applicatorA.ApplyPatch(document, patchAdd);
        
        // Assert
        model.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Converge_WhenApplyingConcurrentAddsAndRemoves_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A", "B" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);

        // Replica A removes "B"
        var patchRemoveB = patcherA.GeneratePatch(
            ancestorDocument,
            new TestModel { Tags = { "A" } });

        // Replica B adds "C"
        var patchAddC = patcherB.GeneratePatch(
            ancestorDocument,
            new TestModel { Tags = { "A", "B", "C" } });
        
        // Scenario 1: Remove then Add
        var model1 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta1 = metadataManagerA.Initialize(model1);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchRemoveB);
        applicatorA.ApplyPatch(doc1, patchAddC);

        // Scenario 2: Add then Remove
        var model2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta2 = metadataManagerA.Initialize(model2);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchAddC);
        applicatorA.ApplyPatch(doc2, patchRemoveB);

        // Assert
        var expected = new[] { "A", "C" };
        model1.Tags.ShouldBe(expected, ignoreOrder: true);
        model2.Tags.ShouldBe(expected, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentOps_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A", "B" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        var patcherC = patcherB;

        // Replica A removes B
        var patch1 = patcherA.GeneratePatch(
            ancestorDocument,
            new TestModel { Tags = { "A" } });
        
        // Replica B adds C
        var patch2 = patcherB.GeneratePatch(
            ancestorDocument,
            new TestModel { Tags = { "A", "B", "C" } });
        
        // Replica C removes A
        var patch3 = patcherC.GeneratePatch(
            ancestorDocument,
            new TestModel { Tags = { "B" } });

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<string>>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new TestModel { Tags = new List<string>(ancestor.Tags) };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in permutation)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalStates.Add(model.Tags);
        }

        // Assert
        // A is removed. B is removed. C is added.
        // Final state: "C"
        var expected = new[] { "C" };
        var firstState = finalStates.First();
        firstState.ShouldBe(expected, ignoreOrder: true);
        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState, ignoreOrder: true);
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