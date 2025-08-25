namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

public sealed class FixedSizeArrayStrategyTests
{
    private sealed class TestModel
    {
        [CrdtFixedSizeArrayStrategy(3)]
        public List<int> Values { get; set; } = new();
    }

    private readonly CrdtPatcher patcherA;
    private readonly CrdtPatcher patcherB;
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;

    public FixedSizeArrayStrategyTests()
    {
        var timestampProvider = new EpochTimestampProvider();
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());

        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });
        var optionsB = Options.Create(new CrdtOptions { ReplicaId = "B" });

        var lwwStrategy = new LwwStrategy(optionsA);
        var fixedSizeArrayStrategyA = new FixedSizeArrayStrategy(timestampProvider, optionsA);
        var strategiesA = new ICrdtStrategy[] { lwwStrategy, fixedSizeArrayStrategyA };
        var strategyManagerA = new CrdtStrategyManager(strategiesA);
        patcherA = new CrdtPatcher(strategyManagerA);

        var lwwStrategyB = new LwwStrategy(optionsB);
        var fixedSizeArrayStrategyB = new FixedSizeArrayStrategy(timestampProvider, optionsB);
        var strategiesB = new ICrdtStrategy[] { lwwStrategyB, fixedSizeArrayStrategyB };
        var strategyManagerB = new CrdtStrategyManager(strategiesB);
        patcherB = new CrdtPatcher(strategyManagerB);
        
        var strategyManagerApplicator = new CrdtStrategyManager(strategiesA);
        applicator = new CrdtApplicator(strategyManagerApplicator);
        metadataManager = new CrdtMetadataManager(strategyManagerA, timestampProvider, comparerProvider);
    }

    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForChangedElement()
    {
        // Arrange
        var doc1 = new TestModel { Values = [1, 2, 3] };
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Values = [1, 99, 3] };
        var meta2 = metadataManager.Initialize(doc2);
        
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
        var initialMeta = metadataManager.Initialize(initialModel);

        var modifiedModel = new TestModel { Values = [1, 99, 3] };
        var modifiedMeta = CloneMetadata(initialMeta);
        var patch = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(initialModel, initialMeta),
            new CrdtDocument<TestModel>(modifiedModel, modifiedMeta));

        var targetModel = new TestModel { Values = new List<int>(initialModel.Values) };
        var targetMeta = CloneMetadata(initialMeta);

        // Act
        patch.Operations.ShouldHaveSingleItem();
        applicator.ApplyPatch(targetModel, patch, targetMeta);
        var stateAfterFirstApply = new List<int>(targetModel.Values);
        
        applicator.ApplyPatch(targetModel, patch, targetMeta);

        // Assert
        targetModel.Values.ShouldBe(stateAfterFirstApply);
        targetModel.Values.ShouldBe([1, 99, 3]);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentUpdates_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TestModel { Values = [10, 20, 30] };
        var metaAncestor = metadataManager.Initialize(ancestor);

        // Replica A updates index 0
        var metaForA = CloneMetadata(metaAncestor);
        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [11, 20, 30] }, metaForA));

        // Replica B updates index 2
        Thread.Sleep(15); // Use a longer sleep to ensure timestamp is different on all systems
        var metaForB = CloneMetadata(metaAncestor);
        var patchB = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [10, 20, 33] }, metaForB));
        
        patchA.Operations.ShouldHaveSingleItem();
        patchB.Operations.ShouldHaveSingleItem();

        // Scenario 1: A then B
        var model1 = new TestModel { Values = new List<int>(ancestor.Values) };
        var meta1 = CloneMetadata(metaAncestor);
        applicator.ApplyPatch(model1, patchA, meta1);
        applicator.ApplyPatch(model1, patchB, meta1);

        // Scenario 2: B then A
        var model2 = new TestModel { Values = new List<int>(ancestor.Values) };
        var meta2 = CloneMetadata(metaAncestor);
        applicator.ApplyPatch(model2, patchB, meta2);
        applicator.ApplyPatch(model2, patchA, meta2);

        // Assert
        var expected = new List<int> { 11, 20, 33 };
        model1.Values.ShouldBe(expected);
        model2.Values.ShouldBe(expected);
    }
    
    [Fact]
    public void Converge_OnConflictingUpdate_LwwShouldWin()
    {
        // Arrange
        var ancestor = new TestModel { Values = [0, 0, 0] };
        var metaAncestor = metadataManager.Initialize(ancestor);
        
        // Replica A updates index 1
        var metaForA = CloneMetadata(metaAncestor);
        var patchA = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(new TestModel { Values = [0, 1, 0] }, metaForA));

        // Replica B updates index 1 later in time
        Thread.Sleep(15); // Ensure timestamp is reliably greater
        var metaForB = CloneMetadata(metaAncestor);
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
        var meta = CloneMetadata(metaAncestor);
        applicator.ApplyPatch(model, patchA, meta);
        applicator.ApplyPatch(model, patchB, meta);
        
        // Assert
        model.Values[1].ShouldBe(winningValue);
    }

    private CrdtMetadata CloneMetadata(CrdtMetadata metadata)
    {
        var clone = new CrdtMetadata();
        foreach (var (key, value) in metadata.Lww)
        {
            clone.Lww[key] = value;
        }

        // This is a shallow clone, sufficient for this strategy which only uses the Lww dictionary.
        // A full, deep clone would be required if strategies using other metadata properties were involved.
        return clone;
    }
}