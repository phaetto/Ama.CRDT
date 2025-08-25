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

public sealed class OrSetStrategyTests
{
    private sealed class TestModel
    {
        [CrdtOrSetStrategy]
        public List<string> Tags { get; set; } = new();
    }
    
    private readonly CrdtPatcher patcherA;
    private readonly CrdtPatcher patcherB;
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;
    private readonly TestTimestampProvider timestampProvider;

    public OrSetStrategyTests()
    {
        timestampProvider = new TestTimestampProvider();
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });
        var optionsB = Options.Create(new CrdtOptions { ReplicaId = "B" });
        
        var strategiesA = new ICrdtStrategy[] { new LwwStrategy(optionsA), new OrSetStrategy(comparerProvider, timestampProvider, optionsA) };
        var strategyManagerA = new CrdtStrategyManager(strategiesA);
        patcherA = new CrdtPatcher(strategyManagerA);

        var strategiesB = new ICrdtStrategy[] { new LwwStrategy(optionsB), new OrSetStrategy(comparerProvider, timestampProvider, optionsB) };
        var strategyManagerB = new CrdtStrategyManager(strategiesB);
        patcherB = new CrdtPatcher(strategyManagerB);

        applicator = new CrdtApplicator(strategyManagerA);
        metadataManager = new CrdtMetadataManager(strategyManagerA, timestampProvider, comparerProvider);
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertAndRemoveOpsWithPayloads()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "B", "C" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta1));
        
        // Assert
        patch.Operations.Count.ShouldBe(2);
        var removeOp = patch.Operations.Single(op => op.Type == OperationType.Remove);
        removeOp.Value.ShouldBeOfType<OrSetRemoveItem>();
        ((OrSetRemoveItem)removeOp.Value).Value.ToString().ShouldBe("A");
        ((OrSetRemoveItem)removeOp.Value).Tags.ShouldNotBeEmpty();

        var addOp = patch.Operations.Single(op => op.Type == OperationType.Upsert);
        addOp.Value.ShouldBeOfType<OrSetAddItem>();
        ((OrSetAddItem)addOp.Value).Value.ToString().ShouldBe("C");
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
    public void Converge_ConcurrentAddAndRemove_ShouldKeepItem()
    {
        // Arrange: Start with A. A removes A, B adds A again.
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManager.Initialize(ancestor);
        
        // Replica A removes "A"
        var patchRemove = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor), 
            new CrdtDocument<TestModel>(new TestModel(), metaAncestor));

        // Replica B concurrently adds "A"
        var patchAdd = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(new TestModel(), metaAncestor), 
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));
        
        // Scenario 1: Remove then Add
        var model1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManager.Initialize(model1);
        applicator.ApplyPatch(model1, patchRemove, meta1);
        applicator.ApplyPatch(model1, patchAdd, meta1);
        
        // Scenario 2: Add then Remove
        var model2 = new TestModel { Tags = { "A" } };
        var meta2 = metadataManager.Initialize(model2);
        applicator.ApplyPatch(model2, patchAdd, meta2);
        applicator.ApplyPatch(model2, patchRemove, meta2);

        // Assert
        // The new instance of "A" added by B has a new tag not present in the remove op from A, so it survives.
        model1.Tags.ShouldBe(new[] { "A" });
        model2.Tags.ShouldBe(new[] { "A" });
    }

    [Fact]
    public void Converge_ReAddingItem_ShouldSucceed()
    {
        // Arrange
        var model = new TestModel { Tags = { "A" } };
        var meta = metadataManager.Initialize(model);

        // Remove "A"
        var patchRemove = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(model, meta), 
            new CrdtDocument<TestModel>(new TestModel(), meta));
        applicator.ApplyPatch(model, patchRemove, meta);
        model.Tags.ShouldBeEmpty();
        
        // Add "A" again
        var patchAdd = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(new TestModel(), meta),
            new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, meta));
        
        // Act
        applicator.ApplyPatch(model, patchAdd, meta);

        // Assert
        model.Tags.ShouldBe(new[] { "A" });
    }

    private sealed class TestTimestampProvider : ICrdtTimestampProvider
    {
        private long currentTime = 1;
        public ICrdtTimestamp Now() => new EpochTimestamp(currentTime++);
    }
}