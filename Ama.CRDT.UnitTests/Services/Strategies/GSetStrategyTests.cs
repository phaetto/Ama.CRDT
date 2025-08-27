namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class GSetStrategyTests
{
    private sealed class TestModel
    {
        [CrdtGSetStrategy]
        public List<string> Tags { get; set; } = new();
    }

    private readonly CrdtPatcher patcherA;
    private readonly CrdtPatcher patcherB;
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;

    public GSetStrategyTests()
    {
        var timestampProvider = new EpochTimestampProvider();
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });
        var optionsB = Options.Create(new CrdtOptions { ReplicaId = "B" });
        
        var strategiesA = new ICrdtStrategy[] { new LwwStrategy(optionsA), new GSetStrategy(comparerProvider, timestampProvider, optionsA) };
        var strategyManagerA = new CrdtStrategyProvider(strategiesA);
        patcherA = new CrdtPatcher(strategyManagerA);

        var strategiesB = new ICrdtStrategy[] { new LwwStrategy(optionsB), new GSetStrategy(comparerProvider, timestampProvider, optionsB) };
        var strategyManagerB = new CrdtStrategyProvider(strategiesB);
        patcherB = new CrdtPatcher(strategyManagerB);

        applicator = new CrdtApplicator(strategyManagerA);
        metadataManager = new CrdtMetadataManager(strategyManagerA, timestampProvider, comparerProvider);
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForAddedItem()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A", "B" } };
        var meta2 = metadataManager.Initialize(doc2);
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta2));
        
        // Assert
        patch.Operations.ShouldHaveSingleItem();
        var op = patch.Operations.First();
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.tags");
        op.Value.ShouldBe("B");
    }

    [Fact]
    public void GeneratePatch_ShouldNotCreateOpForRemovedItem()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A" } };
        var meta2 = metadataManager.Initialize(doc2);
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta2));
        
        // Assert
        patch.Operations.ShouldBeEmpty();
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
        
        // Clear SeenExceptions to prove the strategy logic itself is idempotent
        targetMeta.SeenExceptions.Clear();
        applicator.ApplyPatch(targetDocument, patch);
        
        // Assert
        target.Tags.ShouldBe(stateAfterFirst);
        target.Tags.ShouldBe(new[] { "A", "B" }, ignoreOrder: true);
    }
    
    [Fact]
    public void ApplyPatch_ShouldIgnoreRemoveOperations()
    {
        // Arrange
        var doc = new TestModel { Tags = { "A", "B" } };
        var meta = metadataManager.Initialize(doc);
        var document = new CrdtDocument<TestModel>(doc, meta);
        var removeOp = new CrdtOperation(System.Guid.NewGuid(), "A", "$.tags", OperationType.Remove, "A", new EpochTimestamp(1));
        var patch = new CrdtPatch(new List<CrdtOperation> { removeOp });

        // Act
        applicator.ApplyPatch(document, patch);

        // Assert
        doc.Tags.ShouldBe(new[] { "A", "B" });
    }

    [Fact]
    public void Converge_WhenApplyingConcurrentAdds_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManager.Initialize(ancestor);

        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A", "B" } }, metaAncestor));

        var patchB = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A", "C" } }, metaAncestor));
        
        // Scenario 1: A then B
        var model1 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta1 = metadataManager.Initialize(model1);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicator.ApplyPatch(doc1, patchA);
        applicator.ApplyPatch(doc1, patchB);

        // Scenario 2: B then A
        var model2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta2 = metadataManager.Initialize(model2);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicator.ApplyPatch(doc2, patchB);
        applicator.ApplyPatch(doc2, patchA);

        // Assert
        var expected = new[] { "A", "B", "C" };
        model1.Tags.ShouldBe(expected, ignoreOrder: true);
        model2.Tags.ShouldBe(expected, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentAdds_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManager.Initialize(ancestor);
        
        var patcherC = patcherB; // Doesn't matter for this test

        var patch1 = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A", "B" } }, metaAncestor));

        var patch2 = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A", "C" } }, metaAncestor));
        
        var patch3 = patcherC.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A", "D" } }, metaAncestor));

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
        var expected = new[] { "A", "B", "C", "D" };
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