namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

[CrdtSerializable(typeof(ArrayLcsStrategyTests.TestModel))]
[CrdtSerializable(typeof(List<string>))]
internal partial class ArrayLcsTestCrdtContext : CrdtContext
{
}

public sealed class ArrayLcsStrategyTests : IDisposable
{
    internal sealed class TestModel
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
            .AddCrdtAotContext<ArrayLcsTestCrdtContext>()
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
    public void GenerateOperation_WithInsertIntent_ShouldGenerateUpsertWithCorrectPosition()
    {
        // Arrange
        var doc = new TestModel { Tags = new List<string> { "A", "C" } };
        var meta = metadataManagerA.Initialize(doc);
        var propertyInfo = new CrdtPropertyInfo(
            nameof(TestModel.Tags),
            "tags",
            typeof(List<string>),
            true,
            true,
            obj => ((TestModel)obj).Tags,
            (obj, val) => ((TestModel)obj).Tags = (List<string>)val!,
            new CrdtArrayLcsStrategyAttribute(),
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );
        var timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        
        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<ArrayLcsStrategy>().Single();

        var intent = new InsertIntent(1, "B");
        var context = new GenerateOperationContext(doc, meta, "$.tags", propertyInfo, intent, timestampProvider.Now(), 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.tags");
        var item = operation.Value.ShouldBeOfType<PositionalItem>();
        item.Value.ShouldBe("B");
        item.Position.ShouldBe("1.5");
    }

    [Fact]
    public void GenerateOperation_WithRemoveIntent_ShouldGenerateRemoveWithCorrectIdentifier()
    {
        // Arrange
        var doc = new TestModel { Tags = new List<string> { "A", "B", "C" } };
        var meta = metadataManagerA.Initialize(doc);
        var propertyInfo = new CrdtPropertyInfo(
            nameof(TestModel.Tags),
            "tags",
            typeof(List<string>),
            true,
            true,
            obj => ((TestModel)obj).Tags,
            (obj, val) => ((TestModel)obj).Tags = (List<string>)val!,
            new CrdtArrayLcsStrategyAttribute(),
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );
        var timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<ArrayLcsStrategy>().Single();

        var intent = new RemoveIntent(1); // Intent to remove "B"
        var context = new GenerateOperationContext(doc, meta, "$.tags", propertyInfo, intent, timestampProvider.Now(), 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Remove);
        operation.JsonPath.ShouldBe("$.tags");
        var identifier = operation.Value.ShouldBeOfType<PositionalIdentifier>();
        identifier.Position.ShouldBe("2");
    }

    [Fact]
    public void GenerateOperation_WithInvalidIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var doc = new TestModel { Tags = new List<string> { "A" } };
        var meta = metadataManagerA.Initialize(doc);
        var propertyInfo = new CrdtPropertyInfo(
            nameof(TestModel.Tags),
            "tags",
            typeof(List<string>),
            true,
            true,
            obj => ((TestModel)obj).Tags,
            (obj, val) => ((TestModel)obj).Tags = (List<string>)val!,
            new CrdtArrayLcsStrategyAttribute(),
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );
        var timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<ArrayLcsStrategy>().Single();

        var intent = new InvalidIntent();
        var context = new GenerateOperationContext(doc, meta, "$.tags", propertyInfo, intent, timestampProvider.Now(), 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
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
        var targetMeta = initialMeta.DeepClone();
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
            // Clone the metadata to keep the original initialized version vector state
            var meta = metaAncestor.DeepClone();
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
        var finalMeta_1 = metaAncestor.DeepClone();
        var document_1 = new CrdtDocument<TestModel>(finalModel_1, finalMeta_1);
        applicatorA.ApplyPatch(document_1, patch_RemoveB);
        applicatorA.ApplyPatch(document_1, patch_InsertX);

        // Scenario 2: Insert X, then Remove B
        var finalModel_2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var finalMeta_2 = metaAncestor.DeepClone();
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
        var ancestor = new TestModel { Tags = { "A", "C" } };
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManagerA.Initialize(ancestor));


        // 2. Two replicas concurrently insert items at the same logical position.
        var patchB = patcherA.GeneratePatch(
            docAncestor,
            new TestModel { Tags = ["A", "B", "C"] });

        var patchX = patcherB.GeneratePatch(
            docAncestor,
            new TestModel { Tags = ["A", "X", "C"] });

        // 3. Apply these patches to a third replica to create a converged state.
        var docConverged = new CrdtDocument<TestModel>(new TestModel { Tags = new List<string>(ancestor.Tags) }, docAncestor.Metadata.DeepClone());
        applicatorA.ApplyPatch(docConverged, patchB);
        applicatorA.ApplyPatch(docConverged, patchX);

        // Sanity check: "B" and "X" now have the same position string "1.5" in the metadata.
        var positionalTrackers = docConverged.Metadata.PositionalTrackers["$.tags"];
        positionalTrackers.Count(p => p.Position == "1.5").ShouldBe(2);

        // 4. Create a new state by inserting "Y" between "B" and "X".
        var doc_after_Y_insert = new TestModel { Tags = new List<string>(docConverged.Data.Tags) };
        // Order depends on replica IDs, find them to insert deterministically
        var indexB = doc_after_Y_insert.Tags.IndexOf("B");
        var indexX = doc_after_Y_insert.Tags.IndexOf("X");
        doc_after_Y_insert.Tags.Insert(Math.Max(indexB, indexX), "Y");

        // Act
        // 5. Generate a patch for this new insertion. This will call GeneratePositionBetween("1.5", "1.5").
        var patchY = patcherA.GeneratePatch(
            docConverged,
            doc_after_Y_insert);

        // Assert
        // 6. The new operation for "Y" should also have the position "1.5", relying on its OperationId for tie-breaking.
        patchY.Operations.ShouldHaveSingleItem();
        var positionalItem = patchY.Operations.Single().Value.ShouldBeOfType<PositionalItem>();
        positionalItem.Value.ShouldBe("Y");
        positionalItem.Position.ShouldBe("1.5");
    }

    [Fact]
    public void Compact_ShouldNotModifyMetadata_AsStrategyDoesNotMaintainTombstones()
    {
        // Arrange
        var doc = new TestModel { Tags = new List<string> { "A", "B" } };
        var meta = metadataManagerA.Initialize(doc);
        var originalTrackersCount = meta.PositionalTrackers["$.tags"].Count;

        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<ArrayLcsStrategy>().Single();
        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);

        var context = new CompactionContext(meta, mockPolicy.Object, "Tags", "$.tags", doc);

        // Act
        strategy.Compact(context);

        // Assert
        meta.PositionalTrackers["$.tags"].Count.ShouldBe(originalTrackersCount);
        // Verify IsSafeToCompact was never called, as there are no tombstones to check
        mockPolicy.Verify(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()), Times.Never);
    }

    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }

    private readonly record struct InvalidIntent : IOperationIntent;
}