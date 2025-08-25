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
        var strategyManagerA = new CrdtStrategyManager(strategiesA);
        patcherA = new CrdtPatcher(strategyManagerA);

        var strategiesB = new ICrdtStrategy[] { new LwwStrategy(optionsB), new GSetStrategy(comparerProvider, timestampProvider, optionsB) };
        var strategyManagerB = new CrdtStrategyManager(strategiesB);
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
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A", "B" } };
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta1));

        var target = new TestModel { Tags = { "A" } };
        var targetMeta = metadataManager.Initialize(target);
        
        // Act
        applicator.ApplyPatch(target, patch, targetMeta);
        var stateAfterFirst = new List<string>(target.Tags);
        
        applicator.ApplyPatch(target, patch, targetMeta);
        
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
        var removeOp = new CrdtOperation(System.Guid.NewGuid(), "A", "$.tags", OperationType.Remove, "A", new EpochTimestamp(1));
        var patch = new CrdtPatch(new List<CrdtOperation> { removeOp });

        // Act
        applicator.ApplyPatch(doc, patch, meta);

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
        applicator.ApplyPatch(model1, patchA, meta1);
        applicator.ApplyPatch(model1, patchB, meta1);

        // Scenario 2: B then A
        var model2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta2 = metadataManager.Initialize(model2);
        applicator.ApplyPatch(model2, patchB, meta2);
        applicator.ApplyPatch(model2, patchA, meta2);

        // Assert
        var expected = new[] { "A", "B", "C" };
        model1.Tags.ShouldBe(expected, ignoreOrder: true);
        model2.Tags.ShouldBe(expected, ignoreOrder: true);
    }
}