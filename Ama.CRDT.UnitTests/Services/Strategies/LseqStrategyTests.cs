namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Xunit;

public sealed class LseqStrategyTests : IDisposable
{
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;
    private readonly IPartitionableCrdtStrategy lseqStrategy;
    private readonly PropertyInfo itemsProperty;

    public LseqStrategyTests()
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
        
        var strategyProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();
        itemsProperty = typeof(LseqTestModel).GetProperty(nameof(LseqTestModel.Items))!;
        lseqStrategy = (IPartitionableCrdtStrategy)strategyProvider.GetStrategy(itemsProperty);
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
        scopeC.Dispose();
    }

    private class LseqTestModel
    {
        [CrdtLseqStrategy]
        public List<string> Items { get; set; } = new();
    }

    [Fact]
    public void ApplyPatch_WithConcurrentInserts_ShouldConverge()
    {
        // Arrange
        var doc0 = new LseqTestModel { Items = ["A", "C"] };
        var meta0 = metadataManagerA.Initialize(doc0);
        var crdtDoc0 = new CrdtDocument<LseqTestModel>(doc0, meta0);

        var docA = new LseqTestModel { Items = ["A", "B", "C"] };
        var docB = new LseqTestModel { Items = ["A", "D", "C"] };

        // The modified document uses the original's metadata as a base context for the diff.
        var patchA = patcherA.GeneratePatch(crdtDoc0, docA);
        var patchB = patcherB.GeneratePatch(crdtDoc0, docB);
        
        // Act
        // Path 1: Apply A then B
        var doc1 = new CrdtDocument<LseqTestModel>(
            new LseqTestModel { Items = ["A", "C"] },
            metadataManagerA.Initialize(new LseqTestModel { Items = ["A", "C"] })
        );
        applicatorA.ApplyPatch(doc1, patchA);
        applicatorA.ApplyPatch(doc1, patchB);

        // Path 2: Apply B then A
        var doc2 = new CrdtDocument<LseqTestModel>(
            new LseqTestModel { Items = ["A", "C"] },
            metadataManagerA.Initialize(new LseqTestModel { Items = ["A", "C"] })
        );
        applicatorA.ApplyPatch(doc2, patchB);
        applicatorA.ApplyPatch(doc2, patchA);

        // Assert
        doc1.Data.Items.ShouldNotBeNull();
        doc2.Data.Items.ShouldNotBeNull();
        
        // The exact order depends on the generated identifiers. We just need to ensure they converge to the same state.
        doc1.Data.Items.Count.ShouldBe(4);
        var sortedResult1 = doc1.Data.Items.OrderBy(x => x).ToList();
        
        sortedResult1.ShouldBe(new List<string> { "A", "B", "C", "D" });
        doc1.Data.Items.ShouldBe(doc2.Data.Items);
    }

    [Fact]
    public void ApplyPatch_IsTrulyIdempotent()
    {
        // Arrange
        var original = new LseqTestModel { Items = ["A", "B"] };
        var modified = new LseqTestModel { Items = ["A", "C", "B"] };
        var document = new CrdtDocument<LseqTestModel>(original, metadataManagerA.Initialize(original));
        
        var patch = patcherA.GeneratePatch(document, modified);

        // Act
        applicatorA.ApplyPatch(document, patch);
        var stateAfterFirstApply = document.Data.Items.ToList();
        
        // Clear seen exceptions to test the strategy's own idempotency
        document.Metadata.SeenExceptions.Clear();
        applicatorA.ApplyPatch(document, patch);

        // Assert
        stateAfterFirstApply.ShouldBe(new List<string> { "A", "C", "B" });
        document.Data.Items.ShouldBe(stateAfterFirstApply);
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentOps_ShouldBeAssociative()
    {
        // Arrange
        var doc0 = new LseqTestModel { Items = ["A", "D"] };
        var crdtDoc0 = new CrdtDocument<LseqTestModel>(doc0, metadataManagerA.Initialize(doc0));

        var patchB = patcherA.GeneratePatch(crdtDoc0, new LseqTestModel { Items = ["A", "B", "D"] });
        var patchC = patcherB.GeneratePatch(crdtDoc0, new LseqTestModel { Items = ["A", "C", "D"] });
        var patchE = patcherC.GeneratePatch(crdtDoc0, new LseqTestModel { Items = ["A", "E", "D"] });

        var patches = new[] { patchB, patchC, patchE };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<string>>();

        // Act
        foreach (var p in permutations)
        {
            var model = new LseqTestModel { Items = ["A", "D"] };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<LseqTestModel>(model, meta);
            foreach (var patch in p)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalStates.Add(model.Items);
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
    public void GetMinimumKey_ShouldReturnEmptyLseqIdentifier()
    {
        // Act
        var minKey = lseqStrategy.GetMinimumKey(itemsProperty);

        // Assert
        minKey.ShouldBeOfType<LseqIdentifier>();
        var lseqId = (LseqIdentifier)minKey;
        lseqId.Path.ShouldBeEmpty();
    }

    [Fact]
    public void GetStartKey_ShouldReturnNull()
    {
        // Arrange
        var doc = new LseqTestModel { Items = ["A", "B", "C"] };

        // Act
        var startKey = lseqStrategy.GetStartKey(doc, itemsProperty);

        // Assert
        startKey.ShouldBeNull();
    }

    [Fact]
    public void GetKeyFromOperation_WithUpsert_ShouldExtractLseqIdentifier()
    {
        // Arrange
        var path = ImmutableList.Create(new LseqPathSegment(5, "ReplicaA"));
        var id = new LseqIdentifier(path);
        var item = new LseqItem(id, "TestValue");
        var operation = new CrdtOperation(Guid.NewGuid(), "ReplicaA", "$.items", OperationType.Upsert, item, new EpochTimestamp(1), 0);

        // Act
        var key = lseqStrategy.GetKeyFromOperation(operation, "$.items");

        // Assert
        key.ShouldBeOfType<LseqIdentifier>();
        key.ShouldBe(id);
    }

    [Fact]
    public void GetKeyFromOperation_WithRemove_ShouldExtractLseqIdentifier()
    {
        // Arrange
        var path = ImmutableList.Create(new LseqPathSegment(10, "ReplicaB"));
        var id = new LseqIdentifier(path);
        var operation = new CrdtOperation(Guid.NewGuid(), "ReplicaB", "$.items", OperationType.Remove, id, new EpochTimestamp(2), 0);

        // Act
        var key = lseqStrategy.GetKeyFromOperation(operation, "$.items");

        // Assert
        key.ShouldBeOfType<LseqIdentifier>();
        key.ShouldBe(id);
    }

    [Fact]
    public void GetKeyFromOperation_WithMismatchedPath_ShouldReturnNull()
    {
        // Arrange
        var path = ImmutableList.Create(new LseqPathSegment(5, "ReplicaA"));
        var id = new LseqIdentifier(path);
        var item = new LseqItem(id, "TestValue");
        var operation = new CrdtOperation(Guid.NewGuid(), "ReplicaA", "$.otherItems", OperationType.Upsert, item, new EpochTimestamp(1), 0);

        // Act
        var key = lseqStrategy.GetKeyFromOperation(operation, "$.items");

        // Assert
        key.ShouldBeNull();
    }

    [Fact]
    public void Split_WithFourItems_ShouldSplitIntoTwoPartitions()
    {
        // Arrange
        var doc0 = new LseqTestModel { Items = new List<string>() };
        var crdtDoc = new CrdtDocument<LseqTestModel>(doc0, metadataManagerA.Initialize(doc0));
        
        var modified = new LseqTestModel { Items = new List<string> { "Item1", "Item2", "Item3", "Item4" } };
        var patch = patcherA.GeneratePatch(crdtDoc, modified);
        applicatorA.ApplyPatch(crdtDoc, patch);

        // Verify pre-split setup
        crdtDoc.Data.Items.Count.ShouldBe(4);
        crdtDoc.Metadata.LseqTrackers["$.items"].Count.ShouldBe(4);
        var originalIdentifiers = crdtDoc.Metadata.LseqTrackers["$.items"].Select(i => i.Identifier).ToList();

        // Act
        var splitResult = lseqStrategy.Split(crdtDoc.Data, crdtDoc.Metadata, itemsProperty);

        // Assert
        splitResult.SplitKey.ShouldBeOfType<LseqIdentifier>();
        var expectedSplitKey = originalIdentifiers[2]; // index 4 / 2 = 2
        splitResult.SplitKey.ShouldBe(expectedSplitKey);

        var p1Data = (LseqTestModel)splitResult.Partition1.Data;
        var p2Data = (LseqTestModel)splitResult.Partition2.Data;
        
        p1Data.Items.Count.ShouldBe(2);
        p2Data.Items.Count.ShouldBe(2);

        var p1Meta = splitResult.Partition1.Metadata;
        var p2Meta = splitResult.Partition2.Metadata;

        p1Meta.LseqTrackers["$.items"].Count.ShouldBe(2);
        p2Meta.LseqTrackers["$.items"].Count.ShouldBe(2);

        p1Meta.LseqTrackers["$.items"].Select(i => i.Identifier).ShouldBeSubsetOf(originalIdentifiers.Take(2));
        p2Meta.LseqTrackers["$.items"].Select(i => i.Identifier).ShouldBeSubsetOf(originalIdentifiers.Skip(2));
    }

    [Fact]
    public void Merge_WithTwoAdjacentPartitions_ShouldCombineItemsAndMetadata()
    {
        // Arrange
        var doc0 = new LseqTestModel { Items = new List<string>() };
        var crdtDoc = new CrdtDocument<LseqTestModel>(doc0, metadataManagerA.Initialize(doc0));
        var modified = new LseqTestModel { Items = new List<string> { "Alpha", "Beta", "Gamma", "Delta" } };
        var patch = patcherA.GeneratePatch(crdtDoc, modified);
        applicatorA.ApplyPatch(crdtDoc, patch);

        var splitResult = lseqStrategy.Split(crdtDoc.Data, crdtDoc.Metadata, itemsProperty);

        // Act
        var mergedResult = lseqStrategy.Merge(
            splitResult.Partition1.Data, splitResult.Partition1.Metadata, 
            splitResult.Partition2.Data, splitResult.Partition2.Metadata, 
            itemsProperty);

        // Assert
        var mergedData = (LseqTestModel)mergedResult.Data;
        mergedData.Items.Count.ShouldBe(4);
        mergedData.Items.ShouldContain("Alpha");
        mergedData.Items.ShouldContain("Beta");
        mergedData.Items.ShouldContain("Gamma");
        mergedData.Items.ShouldContain("Delta");

        mergedResult.Metadata.LseqTrackers["$.items"].Count.ShouldBe(4);
        mergedResult.Metadata.VersionVector.ShouldNotBeEmpty();
    }

    [Fact]
    public void Split_WithLessThanTwoItems_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var doc0 = new LseqTestModel { Items = new List<string>() };
        var crdtDoc = new CrdtDocument<LseqTestModel>(doc0, metadataManagerA.Initialize(doc0));
        var modified = new LseqTestModel { Items = new List<string> { "SingleItem" } };
        var patch = patcherA.GeneratePatch(crdtDoc, modified);
        applicatorA.ApplyPatch(crdtDoc, patch);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => 
            lseqStrategy.Split(crdtDoc.Data, crdtDoc.Metadata, itemsProperty));
    }

    [Fact]
    public void GenerateOperation_WithAddIntent_ShouldGenerateUpsertOperation()
    {
        // Arrange
        var doc = new LseqTestModel { Items = new List<string>() };
        var meta = metadataManagerA.Initialize(doc);
        var context = new GenerateOperationContext(doc, meta, "$.items", itemsProperty, new AddIntent("NewItem"), new EpochTimestamp(1), 0);

        // Act
        var operation = lseqStrategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.items");
        var item = operation.Value.ShouldBeOfType<LseqItem>();
        item.Value.ShouldBe("NewItem");
        item.Identifier.Path.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateOperation_WithInsertIntent_ShouldGenerateUpsertOperation()
    {
        // Arrange
        var emptyDoc = new CrdtDocument<LseqTestModel>(new LseqTestModel(), metadataManagerA.Initialize(new LseqTestModel()));
        var populatedModel = new LseqTestModel { Items = new List<string> { "A", "C" } };
        var initialPatch = patcherA.GeneratePatch(emptyDoc, populatedModel);
        
        var crdtDoc = new CrdtDocument<LseqTestModel>(new LseqTestModel(), metadataManagerA.Initialize(new LseqTestModel()));
        applicatorA.ApplyPatch(crdtDoc, initialPatch);
        
        var context = new GenerateOperationContext(crdtDoc.Data, crdtDoc.Metadata, "$.items", itemsProperty, new InsertIntent(1, "B"), new EpochTimestamp(2), 0);

        // Act
        var operation = lseqStrategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        var item = operation.Value.ShouldBeOfType<LseqItem>();
        item.Value.ShouldBe("B");
        
        // Verify application
        lseqStrategy.ApplyOperation(new ApplyOperationContext(crdtDoc.Data, crdtDoc.Metadata, operation));
        crdtDoc.Data.Items.ShouldBe(new List<string> { "A", "B", "C" });
    }

    [Fact]
    public void GenerateOperation_WithRemoveIntent_ShouldGenerateRemoveOperation()
    {
        // Arrange
        var emptyDoc = new CrdtDocument<LseqTestModel>(new LseqTestModel(), metadataManagerA.Initialize(new LseqTestModel()));
        var populatedModel = new LseqTestModel { Items = new List<string> { "A", "B", "C" } };
        var initialPatch = patcherA.GeneratePatch(emptyDoc, populatedModel);
        
        var crdtDoc = new CrdtDocument<LseqTestModel>(new LseqTestModel(), metadataManagerA.Initialize(new LseqTestModel()));
        applicatorA.ApplyPatch(crdtDoc, initialPatch);
        
        var context = new GenerateOperationContext(crdtDoc.Data, crdtDoc.Metadata, "$.items", itemsProperty, new RemoveIntent(1), new EpochTimestamp(2), 0);

        // Act
        var operation = lseqStrategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Remove);
        operation.Value.ShouldBeOfType<LseqIdentifier>();
        
        // Verify application
        lseqStrategy.ApplyOperation(new ApplyOperationContext(crdtDoc.Data, crdtDoc.Metadata, operation));
        crdtDoc.Data.Items.ShouldBe(new List<string> { "A", "C" });
    }

    [Fact]
    public void GenerateOperation_WithOutOfBoundsIntent_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var doc = new LseqTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var insertContext = new GenerateOperationContext(doc, meta, "$.items", itemsProperty, new InsertIntent(5, "X"), new EpochTimestamp(1), 0);
        var removeContext = new GenerateOperationContext(doc, meta, "$.items", itemsProperty, new RemoveIntent(5), new EpochTimestamp(1), 0);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => lseqStrategy.GenerateOperation(insertContext));
        Should.Throw<ArgumentOutOfRangeException>(() => lseqStrategy.GenerateOperation(removeContext));
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var doc = new LseqTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var context = new GenerateOperationContext(doc, meta, "$.items", itemsProperty, new SetIntent("Invalid"), new EpochTimestamp(1), 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => lseqStrategy.GenerateOperation(context));
    }

    [Fact]
    public void Compact_ShouldNotModifyMetadata_AsStrategyDoesNotMaintainTombstones()
    {
        // Arrange
        var doc = new LseqTestModel { Items = new List<string> { "A", "B" } };
        var meta = metadataManagerA.Initialize(doc);
        
        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<ICrdtTimestamp>())).Returns(true);

        var context = new CompactionContext(meta, mockPolicy.Object, "Items", "$.items", doc);

        // Act
        lseqStrategy.Compact(context);

        // Assert
        mockPolicy.Verify(p => p.IsSafeToCompact(It.IsAny<ICrdtTimestamp>()), Times.Never);
        meta.LseqTrackers.ShouldNotBeNull();
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