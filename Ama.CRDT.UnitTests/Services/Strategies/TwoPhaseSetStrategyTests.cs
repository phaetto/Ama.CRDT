namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class TwoPhaseSetStrategyTests
{
    private sealed class TestModel
    {
        [CrdtTwoPhaseSetStrategy]
        public List<string> Tags { get; set; } = new();
    }
    
    private readonly CrdtPatcher patcherA;
    private readonly CrdtPatcher patcherB;
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;

    public TwoPhaseSetStrategyTests()
    {
        var timestampProvider = new EpochTimestampProvider();
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });
        var optionsB = Options.Create(new CrdtOptions { ReplicaId = "B" });
        
        var strategiesA = new ICrdtStrategy[] { new LwwStrategy(optionsA), new TwoPhaseSetStrategy(comparerProvider, timestampProvider, optionsA) };
        var strategyManagerA = new CrdtStrategyManager(strategiesA);
        patcherA = new CrdtPatcher(strategyManagerA);

        var strategiesB = new ICrdtStrategy[] { new LwwStrategy(optionsB), new TwoPhaseSetStrategy(comparerProvider, timestampProvider, optionsB) };
        var strategyManagerB = new CrdtStrategyManager(strategiesB);
        patcherB = new CrdtPatcher(strategyManagerB);

        applicator = new CrdtApplicator(strategyManagerA);
        metadataManager = new CrdtMetadataManager(strategyManagerA, timestampProvider, comparerProvider);
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertAndRemoveOps()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "B", "C" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta1));
        
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
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A", "B" } };
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta1));

        var target = new TestModel { Tags = { "A" } };
        var targetMeta = metadataManager.Initialize(target);
        var targetDocument = new CrdtDocument<TestModel>(target, targetMeta);
        
        // Act
        applicator.ApplyPatch(targetDocument, patch);
        var stateAfterFirst = new List<string>(target.Tags);
        
        // Clear seen exceptions to test the strategy's own idempotency
        targetMeta.SeenExceptions.Clear();
        applicator.ApplyPatch(targetDocument, patch);
        
        // Assert
        target.Tags.ShouldBe(stateAfterFirst);
        target.Tags.ShouldBe(new[] { "A", "B" }, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_WhenItemIsRemoved_ItCannotBeReAdded()
    {
        // Arrange
        var model = new TestModel { Tags = { "A" } };
        var meta = metadataManager.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        
        // Remove "A"
        var patchRemove = patcherA.GeneratePatch(document, new CrdtDocument<TestModel>(new TestModel(), meta));
        applicator.ApplyPatch(document, patchRemove);
        model.Tags.ShouldBeEmpty();
        
        // Try to add "A" back
        var patchAdd = patcherA.GeneratePatch(new CrdtDocument<TestModel>(new TestModel(), meta), new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, meta));
        
        // Act
        applicator.ApplyPatch(document, patchAdd);
        
        // Assert
        model.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Converge_WhenApplyingConcurrentAddsAndRemoves_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A", "B" } };
        var metaAncestor = metadataManager.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);

        // Replica A removes "B"
        var patchRemoveB = patcherA.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));

        // Replica B adds "C"
        var patchAddC = patcherB.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A", "B", "C" } }, metaAncestor));
        
        // Scenario 1: Remove then Add
        var model1 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta1 = metadataManager.Initialize(model1);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicator.ApplyPatch(doc1, patchRemoveB);
        applicator.ApplyPatch(doc1, patchAddC);

        // Scenario 2: Add then Remove
        var model2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta2 = metadataManager.Initialize(model2);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicator.ApplyPatch(doc2, patchAddC);
        applicator.ApplyPatch(doc2, patchRemoveB);

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
        var metaAncestor = metadataManager.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        var patcherC = patcherB;

        // Replica A removes B
        var patch1 = patcherA.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));
        
        // Replica B adds C
        var patch2 = patcherB.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A", "B", "C" } }, metaAncestor));
        
        // Replica C removes A
        var patch3 = patcherC.GeneratePatch(
            ancestorDocument,
            new CrdtDocument<TestModel>(new TestModel { Tags = { "B" } }, metaAncestor));

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<string>>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new TestModel { Tags = new List<string>(ancestor.Tags) };
            var meta = metadataManager.Initialize(model);
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in permutation)
            {
                applicator.ApplyPatch(document, patch);
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