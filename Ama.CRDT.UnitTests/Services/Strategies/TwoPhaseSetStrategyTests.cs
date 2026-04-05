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

internal sealed class TwoPhaseSetTestModel
{
    [CrdtTwoPhaseSetStrategy]
    public List<string> Tags { get; set; } = new();
}

public sealed class TwoPhaseSetStrategyTests : IDisposable
{
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly TwoPhaseSetStrategy strategyA;
    private readonly ICrdtTimestampProvider timestampProvider;

    public TwoPhaseSetStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<TwoPhaseSetStrategyTestCrdtAotContext>()
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
        strategyA = scopeA.ServiceProvider.GetRequiredService<TwoPhaseSetStrategy>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
        scopeC.Dispose();
    }
    
    private static CrdtPropertyInfo CreatePropertyInfo()
    {
        return new CrdtPropertyInfo(
            "Tags", 
            "tags", 
            typeof(List<string>), 
            true, 
            true,
            obj => ((TwoPhaseSetTestModel)obj).Tags,
            (obj, val) => ((TwoPhaseSetTestModel)obj).Tags = (List<string>)val!,
            new CrdtTwoPhaseSetStrategyAttribute(),
            Array.Empty<CrdtStrategyDecoratorAttribute>());
    }

    [Fact]
    public void GeneratePatch_ShouldCreateUpsertAndRemoveOps()
    {
        // Arrange
        var doc1 = new TwoPhaseSetTestModel { Tags = { "A", "B" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TwoPhaseSetTestModel { Tags = { "B", "C" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TwoPhaseSetTestModel>(doc1, meta1), doc2);
        
        // Assert
        patch.Operations.Count.ShouldBe(2);
        patch.Operations.ShouldContain(op => op.Type == OperationType.Remove && op.Value.ToString() == "A");
        patch.Operations.ShouldContain(op => op.Type == OperationType.Upsert && op.Value.ToString() == "C");
    }
    
    [Fact]
    public void ApplyPatch_IsTrulyIdempotent()
    {
        // Arrange
        var doc1 = new TwoPhaseSetTestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TwoPhaseSetTestModel { Tags = { "A", "B" } };
        var patch = patcherA.GeneratePatch(new CrdtDocument<TwoPhaseSetTestModel>(doc1, meta1), doc2);

        var target = new TwoPhaseSetTestModel { Tags = { "A" } };
        var targetMeta = metadataManagerA.Initialize(target);
        var targetDocument = new CrdtDocument<TwoPhaseSetTestModel>(target, targetMeta);
        
        // Act
        applicatorA.ApplyPatch(targetDocument, patch);
        var stateAfterFirst = new List<string>(target.Tags);
        
        // Clear seen exceptions to test the strategy's own idempotency
        targetMeta.SeenExceptions.Clear();
        applicatorA.ApplyPatch(targetDocument, patch);
        
        // Assert
        target.Tags.ShouldBe(stateAfterFirst);
        target.Tags.ShouldBe(new[] { "A", "B" }, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_WhenItemIsRemoved_ItCannotBeReAdded()
    {
        // Arrange
        var model = new TwoPhaseSetTestModel { Tags = { "A" } };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TwoPhaseSetTestModel>(model, meta);
        
        // Remove "A"
        var patchRemove = patcherA.GeneratePatch(document, new TwoPhaseSetTestModel());
        applicatorA.ApplyPatch(document, patchRemove);
        model.Tags.ShouldBeEmpty();
        
        // Try to add "A" back
        var patchAdd = patcherA.GeneratePatch(new CrdtDocument<TwoPhaseSetTestModel>(new TwoPhaseSetTestModel(), meta), new TwoPhaseSetTestModel { Tags = { "A" } });
        
        // Act
        applicatorA.ApplyPatch(document, patchAdd);
        
        // Assert
        model.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Converge_WhenApplyingConcurrentAddsAndRemoves_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TwoPhaseSetTestModel { Tags = { "A", "B" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TwoPhaseSetTestModel>(ancestor, metaAncestor);

        // Replica A removes "B"
        var patchRemoveB = patcherA.GeneratePatch(
            ancestorDocument,
            new TwoPhaseSetTestModel { Tags = { "A" } });

        // Replica B adds "C"
        var patchAddC = patcherB.GeneratePatch(
            ancestorDocument,
            new TwoPhaseSetTestModel { Tags = { "A", "B", "C" } });
        
        // Scenario 1: Remove then Add
        var model1 = new TwoPhaseSetTestModel { Tags = new List<string>(ancestor.Tags) };
        var meta1 = metadataManagerA.Initialize(model1);
        var doc1 = new CrdtDocument<TwoPhaseSetTestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchRemoveB);
        applicatorA.ApplyPatch(doc1, patchAddC);

        // Scenario 2: Add then Remove
        var model2 = new TwoPhaseSetTestModel { Tags = new List<string>(ancestor.Tags) };
        var meta2 = metadataManagerA.Initialize(model2);
        var doc2 = new CrdtDocument<TwoPhaseSetTestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchAddC);
        applicatorA.ApplyPatch(doc2, patchRemoveB);

        // Assert
        var expected = new[] { "A", "C" };
        model1.Tags.ShouldBe(expected, ignoreOrder: true);
        model2.Tags.ShouldBe(expected, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentOps_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TwoPhaseSetTestModel { Tags = { "A", "B" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var ancestorDocument = new CrdtDocument<TwoPhaseSetTestModel>(ancestor, metaAncestor);

        // Replica A removes B
        var patch1 = patcherA.GeneratePatch(
            ancestorDocument,
            new TwoPhaseSetTestModel { Tags = { "A" } });
        
        // Replica B adds C
        var patch2 = patcherB.GeneratePatch(
            ancestorDocument,
            new TwoPhaseSetTestModel { Tags = { "A", "B", "C" } });
        
        // Replica C removes A
        var patch3 = patcherC.GeneratePatch(
            ancestorDocument,
            new TwoPhaseSetTestModel { Tags = { "B" } });

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<string>>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new TwoPhaseSetTestModel { Tags = new List<string>(ancestor.Tags) };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<TwoPhaseSetTestModel>(model, meta);
            foreach (var patch in permutation)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalStates.Add(model.Tags);
        }

        // Assert
        // A is removed. B is removed. C is added.
        // Final state: "C"
        var expected = new[] { "C" };
        var firstState = finalStates.First();
        firstState.ShouldBe(expected, ignoreOrder: true);
        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState, ignoreOrder: true);
        }
    }

    [Fact]
    public void GetStartKey_ShouldReturnSmallestKeyOrNull()
    {
        var propInfo = CreatePropertyInfo();
        
        strategyA.GetStartKey(new TwoPhaseSetTestModel(), propInfo).ShouldBeNull();
        strategyA.GetStartKey(new TwoPhaseSetTestModel { Tags = { "c", "a", "b" } }, propInfo).ShouldBe("a");
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractCorrectly()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "myVal", timestampProvider.Now(), 0);
        
        strategyA.GetKeyFromOperation(op, "$.tags").ShouldBe("myVal");
        strategyA.GetKeyFromOperation(op, "$.otherPath").ShouldBeNull();
    }

    [Fact]
    public void GetMinimumKey_ShouldReturnCorrectMinValue()
    {
        var propInfo = CreatePropertyInfo();
        strategyA.GetMinimumKey(propInfo).ShouldBe(string.Empty);
    }

    [Fact]
    public void Split_ShouldDivideDataAndMetadataEqually()
    {
        var doc = new TwoPhaseSetTestModel();
        var meta = metadataManagerA.Initialize(doc);
        var propInfo = CreatePropertyInfo();

        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "a", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "b", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "c", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "d", timestampProvider.Now(), 0)));

        var result = strategyA.Split(doc, meta, propInfo);

        result.SplitKey.ShouldBe("c");

        var doc1 = (TwoPhaseSetTestModel)result.Partition1.Data;
        var doc2 = (TwoPhaseSetTestModel)result.Partition2.Data;

        doc1.Tags.ShouldBe(["a", "b"], ignoreOrder: true);
        doc2.Tags.ShouldBe(["c", "d"], ignoreOrder: true);

        ((TwoPhaseSetState)result.Partition1.Metadata.States["$.tags"]).Adds.ShouldContain("a");
        ((TwoPhaseSetState)result.Partition2.Metadata.States["$.tags"]).Adds.ShouldContain("c");
    }

    [Fact]
    public void Merge_ShouldCombineDataAndMetadata()
    {
        var doc1 = new TwoPhaseSetTestModel();
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TwoPhaseSetTestModel();
        var meta2 = metadataManagerA.Initialize(doc2);
        var propInfo = CreatePropertyInfo();

        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "a", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "b", timestampProvider.Now(), 0)));
        
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "c", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "d", timestampProvider.Now(), 0)));

        var result = strategyA.Merge(doc1, meta1, doc2, meta2, propInfo);

        var mergedDoc = (TwoPhaseSetTestModel)result.Data;
        mergedDoc.Tags.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
        ((TwoPhaseSetState)result.Metadata.States["$.tags"]).Adds.Count.ShouldBeGreaterThanOrEqualTo(4);
    }
    
    [Fact]
    public void GenerateOperation_WithAddIntent_ShouldReturnUpsertOperation()
    {
        // Arrange
        var intent = new AddIntent("X");
        var context = new GenerateOperationContext(null!, null!, "$.tags", null!, intent, timestampProvider.Now(), 0);

        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe("X");
        op.JsonPath.ShouldBe("$.tags");
    }

    [Fact]
    public void GenerateOperation_WithRemoveValueIntent_ShouldReturnRemoveOperation()
    {
        // Arrange
        var intent = new RemoveValueIntent("Y");
        var context = new GenerateOperationContext(null!, null!, "$.tags", null!, intent, timestampProvider.Now(), 0);

        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBe("Y");
        op.JsonPath.ShouldBe("$.tags");
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrow()
    {
        // Arrange
        var intent = new SetIntent("Z");
        var context = new GenerateOperationContext(null!, null!, "$.tags", null!, intent, timestampProvider.Now(), 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void IntentBuilder_ShouldGenerateAndApplyOperationsCorrectly()
    {
        // Arrange
        var doc1 = new TwoPhaseSetTestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var document = new CrdtDocument<TwoPhaseSetTestModel>(doc1, meta1);

        // Act
        // Create and apply the first operation to increment the local clock
        var addOp = patcherA.GenerateOperation(document, x => x.Tags, new AddIntent("B"));
        applicatorA.ApplyPatch(document, new CrdtPatch([addOp]));
        
        // Create and apply the second operation with the updated metadata clock state
        var removeOp = patcherA.GenerateOperation(document, x => x.Tags, new RemoveValueIntent("A"));
        applicatorA.ApplyPatch(document, new CrdtPatch([removeOp]));

        // Assert
        doc1.Tags.ShouldBe(["B"], ignoreOrder: true);
        ((TwoPhaseSetState)meta1.States["$.tags"]).Adds.ShouldContain("B");
        ((TwoPhaseSetState)meta1.States["$.tags"]).Tombstones.ShouldContainKey("A");
    }

    [Fact]
    public void Compact_ShouldRemoveTombstones_WhenPolicyAllows()
    {
        // Arrange
        var mockPolicy = new Mock<ICompactionPolicy>();

        // Mock policy: Safe to compact if ReplicaId == "R1" and Version <= 5
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()))
            .Returns((CompactionCandidate c) => c.ReplicaId == "R1" && c.Version <= 5);

        var metadata = new CrdtMetadata();
        var state = new TwoPhaseSetState(
            new HashSet<object>(),
            new Dictionary<object, CausalTimestamp>()
        );

        state.Tombstones["A"] = new CausalTimestamp(timestampProvider.Now(), "R1", 5); // Should be removed
        state.Tombstones["B"] = new CausalTimestamp(timestampProvider.Now(), "R2", 10); // Should be kept
        state.Tombstones["C"] = new CausalTimestamp(timestampProvider.Now(), "R1", 6); // Should be kept

        metadata.States["$.tags"] = state;

        var context = new CompactionContext(metadata, mockPolicy.Object, "Tags", "$.tags", new TwoPhaseSetTestModel());

        // Act
        strategyA.Compact(context);

        // Assert
        state.Tombstones.ShouldNotContainKey("A");
        state.Tombstones.ShouldContainKey("B");
        state.Tombstones.ShouldContainKey("C");
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