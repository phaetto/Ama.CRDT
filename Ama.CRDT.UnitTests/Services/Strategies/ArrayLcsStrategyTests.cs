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
using Xunit;

public sealed class ArrayLcsStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtArrayLcsStrategy]
        public List<string> Tags { get; set; } = new();
    }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;

    public ArrayLcsStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, SequentialTimestampProvider>()
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
    public void GeneratePatch_ShouldCreateUpsertForAddedItemInMiddle()
    {
        // Arrange
        var doc1 = new TestModel { Tags = new List<string> { "A", "C" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = new List<string> { "A", "B", "C" } };
        
        var crdtDoc1 = new CrdtDocument<TestModel>(doc1, meta1);

        // Act
        var patch = patcherA.GeneratePatch(crdtDoc1, doc2);
        
        // Assert
        patch.Operations.ShouldHaveSingleItem();
        var op = patch.Operations.First();
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.tags");
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var initialModel = new TestModel { Tags = new List<string> { "A", "C" } };
        var initialMeta = metadataManagerA.Initialize(initialModel);

        var modifiedModel = new TestModel { Tags = new List<string> { "A", "B", "C" } };
        var patch = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(initialModel, initialMeta),
            modifiedModel);

        var targetModel = new TestModel { Tags = new List<string>(initialModel.Tags) };
        var targetMeta = metadataManagerA.Clone(initialMeta);
        var targetDocument = new CrdtDocument<TestModel>(targetModel, targetMeta);

        // Act
        applicatorA.ApplyPatch(targetDocument, patch);
        var stateAfterFirstApply = new List<string>(targetModel.Tags);
        var trackersCountAfterFirstApply = targetMeta.PositionalTrackers["$.tags"].Count;

        applicatorA.ApplyPatch(targetDocument, patch);

        // Assert
        targetModel.Tags.ShouldBe(stateAfterFirstApply);
        targetMeta.PositionalTrackers["$.tags"].Count.ShouldBe(trackersCountAfterFirstApply);
        targetModel.Tags.ShouldBe(new[] { "A", "B", "C" });
    }

    [Fact]
    public void Converge_WhenApplyingConcurrentInsertions_ShouldBeCommutativeAndAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = new List<string> { "A", "D" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);

        // Replicas generate patches concurrently from the same state
        var patchA = patcherA.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "B", "D" } });

        var patchB = patcherB.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "C", "D" } });

        var patchC = patcherC.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "X", "D" } });

        var patches = new[] { patchA, patchB, patchC };
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
        var firstState = finalStates.First();
        firstState.Count.ShouldBe(5);
        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState, ignoreOrder: false);
        }
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentInsertAndRemove_ShouldResultInSameFinalState()
    {
        // Arrange
        var ancestor = new TestModel { Tags = new List<string> { "A", "B", "C" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);

        // Replica A removes "B"
        var replicaA = new TestModel { Tags = new List<string> { "A", "C" } };
        var patch_RemoveB = patcherA.GeneratePatch(docAncestor, replicaA);

        // Replica B inserts "X" after "B"
        var replicaB = new TestModel { Tags = new List<string> { "A", "B", "X", "C" } };
        var patch_InsertX = patcherB.GeneratePatch(docAncestor, replicaB);

        // Act
        // Scenario 1: Remove B, then Insert X
        var finalModel_1 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var finalMeta_1 = metadataManagerA.Initialize(finalModel_1);
        var document_1 = new CrdtDocument<TestModel>(finalModel_1, finalMeta_1);
        applicatorA.ApplyPatch(document_1, patch_RemoveB);
        applicatorA.ApplyPatch(document_1, patch_InsertX);

        // Scenario 2: Insert X, then Remove B
        var finalModel_2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var finalMeta_2 = metadataManagerA.Initialize(finalModel_2);
        var document_2 = new CrdtDocument<TestModel>(finalModel_2, finalMeta_2);
        applicatorA.ApplyPatch(document_2, patch_InsertX);
        applicatorA.ApplyPatch(document_2, patch_RemoveB);

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
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManagerA.Initialize(ancestor));


        // 2. Two replicas concurrently insert items at the same logical position.
        var patchB = patcherA.GeneratePatch(
            docAncestor,
            new TestModel { Tags = ["A", "B", "C"] });

        var patchX = patcherB.GeneratePatch(
            docAncestor,
            new TestModel { Tags = ["A", "X", "C"] });

        // 3. Apply these patches to a third replica to create a converged state.
        var doc_converged = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta_converged = metadataManagerA.Initialize(doc_converged);
        var document_converged = new CrdtDocument<TestModel>(doc_converged, meta_converged);
        applicatorA.ApplyPatch(document_converged, patchB);
        applicatorA.ApplyPatch(document_converged, patchX);

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
            document_converged,
            doc_after_Y_insert);

        // Assert
        // 6. The new operation for "Y" should also have the position "1.5", relying on its OperationId for tie-breaking.
        patchY.Operations.ShouldHaveSingleItem();
        var opY = patchY.Operations.Single();
        var positionalItem = (PositionalItem)opY.Value!;
        positionalItem.Value.ShouldBe("Y");
        positionalItem.Position.ShouldBe("1.5");
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