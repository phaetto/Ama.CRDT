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

public sealed class LwwSetStrategyTests
{
    private sealed class TestModel
    {
        [CrdtLwwSetStrategy]
        public List<string> Tags { get; set; } = new();
    }
    
    private readonly CrdtPatcher patcherA;
    private readonly CrdtPatcher patcherB;
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;
    private readonly TestTimestampProvider timestampProvider;

    public LwwSetStrategyTests()
    {
        timestampProvider = new TestTimestampProvider();
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });
        var optionsB = Options.Create(new CrdtOptions { ReplicaId = "B" });
        
        var strategiesA = new ICrdtStrategy[] { new LwwStrategy(optionsA), new LwwSetStrategy(comparerProvider, timestampProvider, optionsA) };
        var strategyManagerA = new CrdtStrategyManager(strategiesA);
        patcherA = new CrdtPatcher(strategyManagerA);

        var strategiesB = new ICrdtStrategy[] { new LwwStrategy(optionsB), new LwwSetStrategy(comparerProvider, timestampProvider, optionsB) };
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
    public void Converge_LastWriteWins_OnAddRemoveConflict()
    {
        // Arrange
        var ancestor = new TestModel();
        var metaAncestor = metadataManager.Initialize(ancestor);
        
        timestampProvider.SetTime(1);
        var patchAdd = patcherA.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));
        
        timestampProvider.SetTime(2);
        var patchRemove = patcherB.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));
        patchRemove = new CrdtPatch(new List<CrdtOperation> { patchRemove.Operations.First() with { Type = OperationType.Remove } }); // Manually create remove

        // Scenario 1: Add (t=1), then Remove (t=2) -> Should be removed
        var model1 = new TestModel();
        var meta1 = metadataManager.Initialize(model1);
        applicator.ApplyPatch(model1, patchAdd, meta1);
        applicator.ApplyPatch(model1, patchRemove, meta1);
        
        // Scenario 2: Remove (t=2), then Add (t=1) -> Should be removed
        var model2 = new TestModel();
        var meta2 = metadataManager.Initialize(model2);
        applicator.ApplyPatch(model2, patchRemove, meta2);
        applicator.ApplyPatch(model2, patchAdd, meta2);

        // Assert
        model1.Tags.ShouldBeEmpty();
        model2.Tags.ShouldBeEmpty();
    }
    
    [Fact]
    public void Converge_LastWriteWins_OnRemoveAddConflict_AllowsReAdd()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManager.Initialize(ancestor);
        
        timestampProvider.SetTime(1);
        var patchRemove = patcherA.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel(), metaAncestor));
        
        timestampProvider.SetTime(2);
        var patchAdd = patcherB.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));
        
        // Scenario 1: Remove (t=1), then Add (t=2) -> Should be present
        var model1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManager.Initialize(model1);
        applicator.ApplyPatch(model1, patchRemove, meta1);
        applicator.ApplyPatch(model1, patchAdd, meta1);
        
        // Scenario 2: Add (t=2), then Remove (t=1) -> Should be present
        var model2 = new TestModel { Tags = { "A" } };
        var meta2 = metadataManager.Initialize(model2);
        applicator.ApplyPatch(model2, patchAdd, meta2);
        applicator.ApplyPatch(model2, patchRemove, meta2);

        // Assert
        model1.Tags.ShouldBe(new[] { "A" });
        model2.Tags.ShouldBe(new[] { "A" });
    }

    private sealed class TestTimestampProvider : ICrdtTimestampProvider
    {
        private long currentTime = 1;
        public ICrdtTimestamp Now() => new EpochTimestamp(currentTime);
        public void SetTime(long time) => currentTime = time;
    }
}