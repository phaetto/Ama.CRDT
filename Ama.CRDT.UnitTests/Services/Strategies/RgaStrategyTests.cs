namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class RgaStrategyTests : IDisposable
{
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;

    public RgaStrategyTests()
    {
        var services = new ServiceCollection()
            .AddCrdt()
            .BuildServiceProvider();

        var factory = services.GetRequiredService<ICrdtScopeFactory>();
        scopeA = factory.CreateScope("A");
        scopeB = factory.CreateScope("B");
        scopeC = factory.CreateScope("C");
        
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

    private class RgaTestModel
    {
        [CrdtRgaStrategy]
        public List<string> Items { get; set; } = new();
    }

    private class DummyIntent : IOperationIntent { }

    [Fact]
    public void ApplyPatch_WithConcurrentInserts_ShouldConverge()
    {
        // Arrange
        var doc0 = new RgaTestModel { Items = ["A", "C"] };
        var meta0 = metadataManagerA.Initialize(doc0);
        var crdtDoc0 = new CrdtDocument<RgaTestModel>(doc0, meta0);

        var docA = new RgaTestModel { Items = ["A", "B", "C"] };
        var docB = new RgaTestModel { Items = ["A", "D", "C"] };

        var patchA = patcherA.GeneratePatch(crdtDoc0, docA);
        var patchB = patcherB.GeneratePatch(crdtDoc0, docB);
        
        // Act
        // Path 1: Apply A then B
        var doc1 = new CrdtDocument<RgaTestModel>(
            new RgaTestModel { Items = ["A", "C"] },
            metadataManagerA.Initialize(new RgaTestModel { Items = ["A", "C"] })
        );
        applicatorA.ApplyPatch(doc1, patchA);
        applicatorA.ApplyPatch(doc1, patchB);

        // Path 2: Apply B then A
        var doc2 = new CrdtDocument<RgaTestModel>(
            new RgaTestModel { Items = ["A", "C"] },
            metadataManagerA.Initialize(new RgaTestModel { Items = ["A", "C"] })
        );
        applicatorA.ApplyPatch(doc2, patchB);
        applicatorA.ApplyPatch(doc2, patchA);

        // Assert
        doc1.Data.Items.ShouldNotBeNull();
        doc2.Data.Items.ShouldNotBeNull();
        
        doc1.Data.Items.Count.ShouldBe(4);
        doc2.Data.Items.Count.ShouldBe(4);

        doc1.Data.Items.ShouldBe(doc2.Data.Items);
    }

    [Fact]
    public void ApplyPatch_WithDeletions_IsTrulyIdempotent()
    {
        // Arrange
        var original = new RgaTestModel { Items = ["A", "B", "C"] };
        var modified = new RgaTestModel { Items = ["A", "C", "D"] };
        var document = new CrdtDocument<RgaTestModel>(original, metadataManagerA.Initialize(original));
        
        var patch = patcherA.GeneratePatch(document, modified);

        // Act
        applicatorA.ApplyPatch(document, patch);
        var stateAfterFirstApply = document.Data.Items.ToList();
        
        document.Metadata.SeenExceptions.Clear();
        applicatorA.ApplyPatch(document, patch);

        // Assert
        stateAfterFirstApply.ShouldBe(new List<string> { "A", "C", "D" });
        document.Data.Items.ShouldBe(stateAfterFirstApply);
        
        // Ensure tombstones exist
        document.Metadata.RgaTrackers["$.items"].Count.ShouldBe(4); // A, B(Deleted), C, D
        document.Metadata.RgaTrackers["$.items"].Count(i => i.IsDeleted).ShouldBe(1);
    }

    [Fact]
    public void GenerateOperation_WithInsertIntent_ShouldInsertCorrectly()
    {
        // Arrange
        var doc = new RgaTestModel { Items = ["A", "C"] };
        var crdtDoc = new CrdtDocument<RgaTestModel>(doc, metadataManagerA.Initialize(doc));

        // Act - Insert at index 1 (between A and C)
        var intent = new InsertIntent(1, "B");
        var operation = patcherA.GenerateOperation(crdtDoc, x => x.Items, intent);
        
        // Wrap the single operation in a patch to apply it
        var patch = new CrdtPatch([operation]);
        applicatorA.ApplyPatch(crdtDoc, patch);

        // Assert
        crdtDoc.Data.Items.ShouldBe(new List<string> { "A", "B", "C" });
        
        // Verify RgaItem structure
        var trackers = crdtDoc.Metadata.RgaTrackers["$.items"];
        trackers.Count.ShouldBe(3);
        var itemB = trackers.First(x => (string)x.Value! == "B");
        var itemA = trackers.First(x => (string)x.Value! == "A");
        itemB.LeftIdentifier.ShouldBe(itemA.Identifier);
    }

    [Fact]
    public void GenerateOperation_WithRemoveIntent_ShouldRemoveCorrectly()
    {
        // Arrange
        var doc = new RgaTestModel { Items = ["A", "B", "C"] };
        var crdtDoc = new CrdtDocument<RgaTestModel>(doc, metadataManagerA.Initialize(doc));

        // Act - Remove index 1 ("B")
        var intent = new RemoveIntent(1);
        var operation = patcherA.GenerateOperation(crdtDoc, x => x.Items, intent);
        
        // Wrap the single operation in a patch to apply it
        var patch = new CrdtPatch([operation]);
        applicatorA.ApplyPatch(crdtDoc, patch);

        // Assert
        crdtDoc.Data.Items.ShouldBe(new List<string> { "A", "C" });
        
        // Verify RgaItem structure (tombstone should be true for B)
        var trackers = crdtDoc.Metadata.RgaTrackers["$.items"];
        trackers.Count.ShouldBe(3);
        var itemB = trackers.First(x => (string)x.Value! == "B");
        itemB.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void GenerateOperation_WithInvalidIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var doc = new RgaTestModel { Items = ["A", "B"] };
        var crdtDoc = new CrdtDocument<RgaTestModel>(doc, metadataManagerA.Initialize(doc));

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => 
            patcherA.GenerateOperation(crdtDoc, x => x.Items, new InsertIntent(5, "X")));
            
        Should.Throw<ArgumentOutOfRangeException>(() => 
            patcherA.GenerateOperation(crdtDoc, x => x.Items, new InsertIntent(-1, "X")));

        Should.Throw<ArgumentOutOfRangeException>(() => 
            patcherA.GenerateOperation(crdtDoc, x => x.Items, new RemoveIntent(2))); // Count is 2, valid indices are 0, 1
            
        Should.Throw<ArgumentOutOfRangeException>(() => 
            patcherA.GenerateOperation(crdtDoc, x => x.Items, new RemoveIntent(-1)));
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var doc = new RgaTestModel { Items = ["A", "B"] };
        var crdtDoc = new CrdtDocument<RgaTestModel>(doc, metadataManagerA.Initialize(doc));

        // Act & Assert
        Should.Throw<NotSupportedException>(() => 
            patcherA.GenerateOperation(crdtDoc, x => x.Items, new DummyIntent()));
    }

    [Fact]
    public void Partitioning_SplitAndMerge_ShouldRestoreOriginalState()
    {
        // Arrange
        var doc = new RgaTestModel { Items = ["A", "B", "C", "D", "E"] };
        var meta = metadataManagerA.Initialize(doc);
        var property = typeof(RgaTestModel).GetProperty(nameof(RgaTestModel.Items))!;
        var strategy = scopeA.ServiceProvider.GetRequiredService<IEnumerable<ICrdtStrategy>>()
            .OfType<RgaStrategy>()
            .First();

        // Act - Split
        var splitResult = strategy.Split(doc, meta, property);

        // Assert Split
        splitResult.SplitKey.ShouldNotBeNull();
        splitResult.SplitKey.ShouldBeOfType<RgaIdentifier>();

        var leftData = (RgaTestModel)splitResult.Partition1.Data;
        var rightData = (RgaTestModel)splitResult.Partition2.Data;

        // Total items should still be 5
        (leftData.Items.Count + rightData.Items.Count).ShouldBe(5);

        // Act - Merge
        var mergedContent = strategy.Merge(
            splitResult.Partition1.Data, splitResult.Partition1.Metadata,
            splitResult.Partition2.Data, splitResult.Partition2.Metadata,
            property);

        var mergedData = (RgaTestModel)mergedContent.Data;

        // Assert Merge
        mergedData.Items.ShouldBe(new List<string> { "A", "B", "C", "D", "E" });
    }

    [Fact]
    public void Partitioning_GetKeyFromOperation_ShouldReturnIdentifier()
    {
        // Arrange
        var property = typeof(RgaTestModel).GetProperty(nameof(RgaTestModel.Items))!;
        var strategy = scopeA.ServiceProvider.GetRequiredService<IEnumerable<ICrdtStrategy>>()
            .OfType<RgaStrategy>()
            .First();

        var id = new RgaIdentifier(12345, "replica1");
        var item = new RgaItem(id, null, "Test", false);
        var upsertOp = new CrdtOperation(Guid.NewGuid(), "replica1", "$.items", OperationType.Upsert, item, new EpochTimestamp(0), 0);
        var removeOp = new CrdtOperation(Guid.NewGuid(), "replica1", "$.items", OperationType.Remove, id, new EpochTimestamp(0), 0);

        // Act & Assert
        strategy.GetKeyFromOperation(upsertOp, "$.items").ShouldBe(id);
        strategy.GetKeyFromOperation(removeOp, "$.items").ShouldBe(id);
        strategy.GetKeyFromOperation(upsertOp, "$.other").ShouldBeNull();
    }

    [Fact]
    public void Compact_ShouldRemoveTombstones_WhenPolicyAllowsAndNoDependenciesExist()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<RgaStrategy>().Single();
        var doc = new RgaTestModel();
        var meta = new CrdtMetadata();

        long safeTicksRoot = DateTime.UnixEpoch.Ticks + 1000 * TimeSpan.TicksPerMillisecond;
        long safeTicksA = DateTime.UnixEpoch.Ticks + 1001 * TimeSpan.TicksPerMillisecond;
        long safeTicksB = DateTime.UnixEpoch.Ticks + 1002 * TimeSpan.TicksPerMillisecond;
        long unsafeTicksC = DateTime.UnixEpoch.Ticks + 2000 * TimeSpan.TicksPerMillisecond;

        var idRoot = new RgaIdentifier(safeTicksRoot, "A");
        var idA = new RgaIdentifier(safeTicksA, "A");
        var idB = new RgaIdentifier(safeTicksB, "A");
        var idC = new RgaIdentifier(unsafeTicksC, "A");
        
        var root = new RgaItem(idRoot, null, "Root", false); // Alive
        var itemA = new RgaItem(idA, idRoot, "A", true); // Dead, Safe, parent=Root
        var itemB = new RgaItem(idB, idA, "B", true); // Dead, Safe, parent=A
        var itemC = new RgaItem(idC, idRoot, "C", true); // Dead, Unsafe, parent=Root

        meta.RgaTrackers["$.items"] = new List<RgaItem> { root, itemA, itemB, itemC };

        var mockPolicy = new Mock<ICompactionPolicy>();
        // Using approximate threshold to filter Safe vs Unsafe
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<ICrdtTimestamp>(ts => ((EpochTimestamp)ts).Value < 2000))).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<ICrdtTimestamp>(ts => ((EpochTimestamp)ts).Value >= 2000))).Returns(false);

        var context = new CompactionContext(meta, mockPolicy.Object, "Items", "$.items", doc);

        // Act
        strategy.Compact(context);

        // Assert
        var trackers = meta.RgaTrackers["$.items"];
        trackers.ShouldContain(i => i.Identifier == idRoot);
        trackers.ShouldNotContain(i => i.Identifier == idA); // Deleted because B is deleted and A is safe
        trackers.ShouldNotContain(i => i.Identifier == idB); // Deleted because safe and no children
        trackers.ShouldContain(i => i.Identifier == idC); // Not deleted because unsafe
    }
}