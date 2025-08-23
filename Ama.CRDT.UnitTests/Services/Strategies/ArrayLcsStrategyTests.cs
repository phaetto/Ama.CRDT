namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class ArrayLcsStrategyTests
{
    private sealed class TestModel
    {
        [CrdtArrayLcsStrategy]
        public List<string> Tags { get; set; } = new();
    }
    
    private readonly CrdtPatcher patcherA;
    private readonly CrdtPatcher patcherB;
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;

    public ArrayLcsStrategyTests()
    {
        var timestampProvider = new EpochTimestampProvider();
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });
        var optionsB = Options.Create(new CrdtOptions { ReplicaId = "B" });

        var lwwStrategyA = new LwwStrategy(optionsA);
        var counterStrategyA = new CounterStrategy(timestampProvider, optionsA);
        var arrayLcsStrategyA = new ArrayLcsStrategy(comparerProvider, timestampProvider, optionsA);
        var strategiesA = new ICrdtStrategy[] { lwwStrategyA, counterStrategyA, arrayLcsStrategyA };
        var strategyManagerA = new CrdtStrategyManager(strategiesA);
        patcherA = new CrdtPatcher(strategyManagerA);

        var lwwStrategyB = new LwwStrategy(optionsB);
        var counterStrategyB = new CounterStrategy(timestampProvider, optionsB);
        var arrayLcsStrategyB = new ArrayLcsStrategy(comparerProvider, timestampProvider, optionsB);
        var strategiesB = new ICrdtStrategy[] { lwwStrategyB, counterStrategyB, arrayLcsStrategyB };
        var strategyManagerB = new CrdtStrategyManager(strategiesB);
        patcherB = new CrdtPatcher(strategyManagerB);
        
        var strategyManagerApplicator = new CrdtStrategyManager(strategiesA);
        applicator = new CrdtApplicator(strategyManagerApplicator);
        metadataManager = new CrdtMetadataManager(strategyManagerA, timestampProvider);
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForAddedItemInMiddle()
    {
        // Arrange
        var doc1 = new TestModel { Tags = new List<string> { "A", "C" } };
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel { Tags = new List<string> { "A", "B", "C" } };
        var meta2 = metadataManager.Initialize(doc2);
        
        var crdtDoc1 = new CrdtDocument<TestModel>(doc1, meta1);
        var crdtDoc2 = new CrdtDocument<TestModel>(doc2, meta2);

        // Act
        var patch = patcherA.GeneratePatch(crdtDoc1, crdtDoc2);
        
        // Assert
        patch.Operations.ShouldHaveSingleItem();
        var op = patch.Operations.First();
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.tags");
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentInsertions_ShouldResultInSameFinalState()
    {
        // Arrange
        var ancestor = new TestModel { Tags = new List<string> { "A", "D" } };
        var metaAncestor = metadataManager.Initialize(ancestor);

        // Replica A inserts "B"
        var replicaA = new TestModel { Tags = new List<string> { "A", "B", "D" } };
        var patchAB = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor), 
            new CrdtDocument<TestModel>(replicaA, metaAncestor));

        // Replica B inserts "C"
        var replicaB = new TestModel { Tags = new List<string> { "A", "C", "D" } };
        var patchAC = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor), 
            new CrdtDocument<TestModel>(replicaB, metaAncestor));

        // Act
        // Scenario 1: Apply B then C
        var finalModel_BC = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var finalMeta_BC = metadataManager.Initialize(finalModel_BC);
        applicator.ApplyPatch(finalModel_BC, patchAB, finalMeta_BC);
        applicator.ApplyPatch(finalModel_BC, patchAC, finalMeta_BC);

        // Scenario 2: Apply C then B
        var finalModel_CB = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var finalMeta_CB = metadataManager.Initialize(finalModel_CB);
        applicator.ApplyPatch(finalModel_CB, patchAC, finalMeta_CB);
        applicator.ApplyPatch(finalModel_CB, patchAB, finalMeta_CB);
        
        // Assert
        // The exact order depends on the generated position and GUID tie-breaker,
        // but both outcomes must be identical.
        finalModel_BC.Tags.ShouldBe(finalModel_CB.Tags);
        finalModel_BC.Tags.Count.ShouldBe(4);
        finalModel_BC.Tags.ShouldContain("A");
        finalModel_BC.Tags.ShouldContain("B");
        finalModel_BC.Tags.ShouldContain("C");
        finalModel_BC.Tags.ShouldContain("D");
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentInsertAndRemove_ShouldResultInSameFinalState()
    {
        // Arrange
        var ancestor = new TestModel { Tags = new List<string> { "A", "B", "C" } };
        var metaAncestor = metadataManager.Initialize(ancestor);

        // Replica A removes "B"
        var replicaA = new TestModel { Tags = new List<string> { "A", "C" } };
        var patch_RemoveB = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor), 
            new CrdtDocument<TestModel>(replicaA, metaAncestor));

        // Replica B inserts "X" after "B"
        var replicaB = new TestModel { Tags = new List<string> { "A", "B", "X", "C" } };
        var patch_InsertX = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(replicaB, metaAncestor));

        // Act
        // Scenario 1: Remove B, then Insert X
        var finalModel_1 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var finalMeta_1 = metadataManager.Initialize(finalModel_1);
        applicator.ApplyPatch(finalModel_1, patch_RemoveB, finalMeta_1);
        applicator.ApplyPatch(finalModel_1, patch_InsertX, finalMeta_1);

        // Scenario 2: Insert X, then Remove B
        var finalModel_2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var finalMeta_2 = metadataManager.Initialize(finalModel_2);
        applicator.ApplyPatch(finalModel_2, patch_InsertX, finalMeta_2);
        applicator.ApplyPatch(finalModel_2, patch_RemoveB, finalMeta_2);

        // Assert
        var expected = new List<string> { "A", "X", "C" };
        finalModel_1.Tags.ShouldBe(expected);
        finalModel_2.Tags.ShouldBe(expected);
    }

    [Fact]
    public void GeneratePatch_WhenInsertingBetweenItemsWithSamePosition_ShouldGenerateSamePosition()
    {
        // Arrange
        // 1. Start with a common ancestor.
        var ancestor = new TestModel { Tags = new List<string> { "A", "C" } };

        // 2. Two replicas concurrently insert items at the same logical position.
        // We create fresh metadata for each to ensure the patch generations are isolated.
        var metaForB = metadataManager.Initialize(ancestor);
        var patchB = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaForB),
            new CrdtDocument<TestModel>(new TestModel { Tags = ["A", "B", "C"] }, metaForB));

        var metaForX = metadataManager.Initialize(ancestor);
        var patchX = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaForX),
            new CrdtDocument<TestModel>(new TestModel { Tags = ["A", "X", "C"] }, metaForX));

        // 3. Apply these patches to a third replica to create a converged state.
        var doc_converged = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta_converged = metadataManager.Initialize(doc_converged);
        applicator.ApplyPatch(doc_converged, patchB, meta_converged);
        applicator.ApplyPatch(doc_converged, patchX, meta_converged);

        // Sanity check: "B" and "X" now have the same position string "1.5" in the metadata.
        var positionalTrackers = meta_converged.PositionalTrackers["$.tags"];
        positionalTrackers.Count(p => p.Position == "1.5").ShouldBe(2);

        // 4. Create a new state by inserting "Y" between "B" and "X".
        var doc_after_Y_insert = new TestModel { Tags = new List<string>(doc_converged.Tags) };
        var index_of_B = doc_after_Y_insert.Tags.IndexOf("B");
        var index_of_X = doc_after_Y_insert.Tags.IndexOf("X");
        
        doc_after_Y_insert.Tags.Insert(Math.Max(index_of_B, index_of_X), "Y");

        // Act
        // 5. Generate a patch for this new insertion. This will call GeneratePositionBetween("1.5", "1.5").
        var patchY = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(doc_converged, meta_converged),
            new CrdtDocument<TestModel>(doc_after_Y_insert, meta_converged));

        // Assert
        // 6. The new operation for "Y" should also have the position "1.5", relying on its OperationId for tie-breaking.
        patchY.Operations.ShouldHaveSingleItem();
        var opY = patchY.Operations.Single();
        var positionalItem = (PositionalItem)opY.Value!;
        positionalItem.Value.ShouldBe("Y");
        positionalItem.Position.ShouldBe("1.5");
    }
}