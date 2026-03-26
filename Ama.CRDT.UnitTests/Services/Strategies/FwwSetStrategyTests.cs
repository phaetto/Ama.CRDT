namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
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
using System.Threading;
using Xunit;

public sealed class FwwSetStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtFwwSetStrategy]
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
    private readonly ICrdtMetadataManager metadataManagerB;
    private readonly ICrdtMetadataManager metadataManagerC;
    private readonly FwwSetStrategy strategyA;
    private readonly ICrdtTimestampProvider timestampProvider;

    public FwwSetStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
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
        metadataManagerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        metadataManagerC = scopeC.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        strategyA = scopeA.ServiceProvider.GetRequiredService<FwwSetStrategy>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
        scopeC.Dispose();
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertAndRemoveOps()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "B", "C" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), doc2);
        
        // Assert
        patch.Operations.Count.ShouldBe(2);
        patch.Operations.ShouldContain(op => op.Type == OperationType.Remove && op.Value.ToString() == "A");
        patch.Operations.ShouldContain(op => op.Type == OperationType.Upsert && op.Value.ToString() == "C");
    }
    
    [Fact]
    public void GenerateOperation_WithAddIntent_ShouldReturnUpsertOperation()
    {
        // Arrange
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new AddIntent("NewTag"), timestampProvider.Now(), 0);

        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe("NewTag");
        op.JsonPath.ShouldBe("$.tags");
        op.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithRemoveValueIntent_ShouldReturnRemoveOperation()
    {
        // Arrange
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new RemoveValueIntent("OldTag"), timestampProvider.Now(), 0);

        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBe("OldTag");
        op.JsonPath.ShouldBe("$.tags");
        op.ReplicaId.ShouldBe("A");
    }
    
    [Fact]
    public void GenerateOperation_WithClearIntent_ShouldReturnRemoveOperation()
    {
        // Arrange
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new ClearIntent(), timestampProvider.Now(), 0);

        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBeNull();
        op.JsonPath.ShouldBe("$.tags");
        op.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrow()
    {
        // Arrange
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new IncrementIntent(1), timestampProvider.Now(), 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_Reset_ShouldClearListAndMetadata()
    {
        // Arrange
        var doc = new TestModel { Tags = { "A" } };
        var meta = metadataManagerA.Initialize(doc);
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.tags", OperationType.Remove, null, timestampProvider.Now(), 0);

        // Act
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, op));

        // Assert
        doc.Tags.ShouldBeEmpty();
        meta.FwwSets.ShouldNotContainKey("$.tags");
    }

    [Fact]
    public void ApplyPatch_IsTrulyIdempotent()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A", "B" } };
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), doc2);

        var target = new TestModel { Tags = { "A" } };
        var targetMeta = metadataManagerA.Initialize(target);
        var targetDocument = new CrdtDocument<TestModel>(target, targetMeta);
        
        // Act
        applicatorA.ApplyPatch(targetDocument, patch);
        var stateAfterFirst = new List<string>(target.Tags);
        
        // Clear seen exceptions to test strategy's own idempotency based on timestamps
        targetMeta.SeenExceptions.Clear();
        applicatorA.ApplyPatch(targetDocument, patch);
        
        // Assert
        target.Tags.ShouldBe(stateAfterFirst);
        target.Tags.ShouldBe(new[] { "A", "B" }, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_FirstWriteWins_OnAddRemoveConflict_KeepsAdd()
    {
        // Arrange
        var ancestor = new TestModel();
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        
        var contextAdd = new GenerateOperationContext(ancestor, metaAncestor, "$.tags", propInfo, new AddIntent("A"), timestampProvider.Create(100), 0);
        var opAdd = strategyA.GenerateOperation(contextAdd);
        
        var contextRemove = new GenerateOperationContext(ancestor, metaAncestor, "$.tags", propInfo, new RemoveValueIntent("A"), timestampProvider.Create(200), 0);
        var opRemove = strategyA.GenerateOperation(contextRemove);

        // Scenario 1: Add (t=100), then Remove (t=200) -> Add wins
        var model1 = new TestModel();
        var meta1 = metadataManagerA.Initialize(model1);
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, opAdd));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, opRemove));
        
        // Scenario 2: Remove (t=200), then Add (t=100) -> Add wins
        var model2 = new TestModel();
        var meta2 = metadataManagerA.Initialize(model2);
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, opRemove));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, opAdd));

        // Assert
        model1.Tags.ShouldBe(new[] { "A" });
        model2.Tags.ShouldBe(new[] { "A" });
    }
    
    [Fact]
    public void Converge_FirstWriteWins_OnRemoveAddConflict_KeepsRemove()
    {
        // Arrange
        var ancestor = new TestModel();
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        
        var contextRemove = new GenerateOperationContext(ancestor, metaAncestor, "$.tags", propInfo, new RemoveValueIntent("A"), timestampProvider.Create(100), 0);
        var opRemove = strategyA.GenerateOperation(contextRemove);
        
        var contextAdd = new GenerateOperationContext(ancestor, metaAncestor, "$.tags", propInfo, new AddIntent("A"), timestampProvider.Create(200), 0);
        var opAdd = strategyA.GenerateOperation(contextAdd);

        // Scenario 1: Remove (t=100), then Add (t=200) -> Remove wins
        var model1 = new TestModel();
        var meta1 = metadataManagerA.Initialize(model1);
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, opRemove));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, opAdd));
        
        // Scenario 2: Add (t=200), then Remove (t=100) -> Remove wins
        var model2 = new TestModel();
        var meta2 = metadataManagerA.Initialize(model2);
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, opAdd));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, opRemove));

        // Assert
        model1.Tags.ShouldBeEmpty();
        model2.Tags.ShouldBeEmpty();
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentOps_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A", "B" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        var model1 = new TestModel { Tags = { "A", "C" } };
        var model2 = new TestModel { Tags = { "B", "D" } };
        var model3 = new TestModel { Tags = { "A", "B", "E" } };

        Thread.Sleep(5);
        var patch1 = patcherA.GeneratePatch(docAncestor, model1); // Remove B, Add C

        Thread.Sleep(5);
        var patch2 = patcherB.GeneratePatch(docAncestor, model2); // Remove A, Add D

        Thread.Sleep(5);
        var patch3 = patcherC.GeneratePatch(docAncestor, model3); // Add E

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<string>>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new TestModel { Tags = new List<string>(ancestor.Tags) };
            var meta = metaAncestor.DeepClone();
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in permutation)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalStates.Add(model.Tags);
        }

        // Assert
        // A and B were initialized at t=0. Any remove will be at t > 0.
        // Therefore, Add(0) <= Remove(>0), and A and B will NEVER be removed in FWW Set!
        // C, D, E are added at t=1, t=2, t=3 with no corresponding removes.
        // Final state must always consistently be: A, B, C, D, E
        var firstState = finalStates.First();
        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState, ignoreOrder:true);
        }
    }

    [Fact]
    public void GetStartKey_ShouldReturnSmallestKeyOrNull()
    {
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        
        strategyA.GetStartKey(new TestModel(), propInfo).ShouldBeNull();
        strategyA.GetStartKey(new TestModel { Tags = { "c", "a", "b" } }, propInfo).ShouldBe("a");
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
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        strategyA.GetMinimumKey(propInfo).ShouldBe(string.Empty);
    }

    [Fact]
    public void Split_ShouldDivideDataAndMetadataEqually()
    {
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;

        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "a", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "b", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "c", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "d", timestampProvider.Now(), 0)));

        var result = strategyA.Split(doc, meta, propInfo);

        result.SplitKey.ShouldBe("c");

        var doc1 = (TestModel)result.Partition1.Data;
        var doc2 = (TestModel)result.Partition2.Data;

        doc1.Tags.ShouldBe(["a", "b"], ignoreOrder: true);
        doc2.Tags.ShouldBe(["c", "d"], ignoreOrder: true);

        result.Partition1.Metadata.FwwSets["$.tags"].Adds.Keys.ShouldContain("a");
        result.Partition2.Metadata.FwwSets["$.tags"].Adds.Keys.ShouldContain("c");
    }

    [Fact]
    public void Merge_ShouldCombineDataAndMetadata()
    {
        var doc1 = new TestModel();
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel();
        var meta2 = metadataManagerA.Initialize(doc2);
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;

        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "a", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "b", timestampProvider.Now(), 0)));
        
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "c", timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "d", timestampProvider.Now(), 0)));

        var result = strategyA.Merge(doc1, meta1, doc2, meta2, propInfo);

        var mergedDoc = (TestModel)result.Data;
        mergedDoc.Tags.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
        result.Metadata.FwwSets["$.tags"].Adds.Keys.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Compact_ShouldRemoveDeadItems_WhenPolicyAllows()
    {
        // Arrange
        var doc = new TestModel();
        var meta = new CrdtMetadata();
        
        var safeTs1 = timestampProvider.Create(10);
        var safeTs2 = timestampProvider.Create(20);
        var unsafeTs = timestampProvider.Create(30);
        
        var adds = new Dictionary<object, ICrdtTimestamp>(EqualityComparer<object>.Default);
        var removes = new Dictionary<object, ICrdtTimestamp>(EqualityComparer<object>.Default);

        // Item 1: Alive (Add < Remove => Add wins)
        adds["item1"] = safeTs1;
        removes["item1"] = safeTs2;

        // Item 2: Dead, Safe (Remove < Add)
        adds["item2"] = safeTs2;
        removes["item2"] = safeTs1;

        // Item 3: Dead, Unsafe Add
        adds["item3"] = unsafeTs;
        removes["item3"] = safeTs1;

        // Item 4: Dead, Safe (No Add)
        removes["item4"] = safeTs1;

        // Item 5: Dead, Unsafe Remove (No Add)
        removes["item5"] = unsafeTs;

        meta.FwwSets["$.tags"] = new LwwSetState(adds, removes);

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(safeTs1)).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(safeTs2)).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(unsafeTs)).Returns(false);

        var context = new CompactionContext(meta, mockPolicy.Object, "Tags", "$.tags", doc);

        // Act
        strategyA.Compact(context);

        // Assert
        meta.FwwSets["$.tags"].Adds.ShouldContainKey("item1");
        meta.FwwSets["$.tags"].Removes.ShouldContainKey("item1");

        meta.FwwSets["$.tags"].Adds.ShouldNotContainKey("item2");
        meta.FwwSets["$.tags"].Removes.ShouldNotContainKey("item2");

        meta.FwwSets["$.tags"].Adds.ShouldContainKey("item3");
        meta.FwwSets["$.tags"].Removes.ShouldContainKey("item3");

        meta.FwwSets["$.tags"].Removes.ShouldNotContainKey("item4");

        meta.FwwSets["$.tags"].Removes.ShouldContainKey("item5");
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