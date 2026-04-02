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
using Xunit;

public sealed class OrSetStrategyTests : IDisposable
{
    internal sealed class TestModel
    {
        [CrdtOrSetStrategy]
        public List<string> Tags { get; set; } = new();
    }
    
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly OrSetStrategy strategyA;

    public OrSetStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<OrSetStrategyTestCrdtContext>()
            .AddSingleton<ICrdtTimestampProvider, TestTimestampProvider>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");
        
        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        strategyA = scopeA.ServiceProvider.GetRequiredService<OrSetStrategy>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertAndRemoveOpsWithPayloads()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "B", "C" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), doc2);
        
        // Assert
        patch.Operations.Count.ShouldBe(2);
        var removeOp = patch.Operations.Single(op => op.Type == OperationType.Remove);
        removeOp.Value.ShouldBeOfType<OrSetRemoveItem>();
        ((OrSetRemoveItem)removeOp.Value).Value!.ToString().ShouldBe("A");
        ((OrSetRemoveItem)removeOp.Value).Tags.ShouldNotBeEmpty();

        var addOp = patch.Operations.Single(op => op.Type == OperationType.Upsert);
        addOp.Value.ShouldBeOfType<OrSetAddItem>();
        ((OrSetAddItem)addOp.Value).Value!.ToString().ShouldBe("C");
    }

    [Fact]
    public void GenerateOperation_AddIntent_ShouldCreateUpsertOp()
    {
        // Arrange
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        var ts = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);

        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new AddIntent("E"), ts.Now(), 0);
        
        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBeOfType<OrSetAddItem>();
        ((OrSetAddItem)op.Value).Value.ShouldBe("E");
        ((OrSetAddItem)op.Value).Tag.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void GenerateOperation_RemoveValueIntent_ShouldCreateRemoveOp()
    {
        // Arrange
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        var ts = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var doc = new TestModel { Tags = { "A" } };
        var meta = metadataManagerA.Initialize(doc);
        
        // Add item normally first to populate tags
        var addContext = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new AddIntent("A"), ts.Now(), 0);
        var addOp = strategyA.GenerateOperation(addContext);
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, addOp));

        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new RemoveValueIntent("A"), ts.Now(), 0);
        
        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBeOfType<OrSetRemoveItem>();
        var payload = (OrSetRemoveItem)op.Value!;
        payload.Value.ShouldBe("A");
        payload.Tags.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateOperation_RemoveIntent_ShouldCreateRemoveOp()
    {
        // Arrange
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        var ts = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        
        var addContext = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new AddIntent("B"), ts.Now(), 0);
        var addOp = strategyA.GenerateOperation(addContext);
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, addOp));

        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new RemoveIntent(0), ts.Now(), 0);
        
        // Act
        var op = strategyA.GenerateOperation(context);

        // Assert
        op.Type.ShouldBe(OperationType.Remove);
        op.Value.ShouldBeOfType<OrSetRemoveItem>();
        var payload = (OrSetRemoveItem)op.Value!;
        payload.Value.ShouldBe("B");
        payload.Tags.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateOperation_UnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        var ts = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);

        var context = new GenerateOperationContext(doc, meta, "$.tags", propInfo, new IncrementIntent(1), ts.Now(), 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
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
        
        // Clear seen exceptions to test the strategy's own idempotency based on tags
        targetMeta.SeenExceptions.Clear();
        applicatorA.ApplyPatch(targetDocument, patch);
        
        // Assert
        target.Tags.ShouldBe(stateAfterFirst);
        target.Tags.ShouldBe(new[] { "A", "B" }, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_ConcurrentAddAndRemove_ShouldKeepItem()
    {
        // Arrange: Start with A. A removes A, B adds A again.
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        
        // Replica A removes "A"
        var patchRemove = patcherA.GeneratePatch(
            docAncestor, 
            new TestModel());

        // Replica B concurrently adds "A"
        var patchAdd = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(new TestModel(), metaAncestor),
            new TestModel { Tags = { "A" } });
        
        // Scenario 1: Remove then Add
        var model1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(model1);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchRemove);
        applicatorA.ApplyPatch(doc1, patchAdd);
        
        // Scenario 2: Add then Remove
        var model2 = new TestModel { Tags = { "A" } };
        var meta2 = metadataManagerA.Initialize(model2);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchAdd);
        applicatorA.ApplyPatch(doc2, patchRemove);

        // Assert
        // The new instance of "A" added by B has a new tag not present in the remove op from A, so it survives.
        model1.Tags.ShouldBe(new[] { "A" });
        model2.Tags.ShouldBe(new[] { "A" });
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentOps_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        
        var patcherC = patcherB; // ReplicaId is what matters

        // Replica A removes "A"
        var patch1 = patcherA.GeneratePatch(
            docAncestor,
            new TestModel());

        // Replica B adds "B"
        var patch2 = patcherB.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "B" } });

        // Replica C adds "A" again
        var patch3 = patcherC.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A" } });
        
        var patches = new[] { patch1, patch2, patch3 };
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
        // The original "A" is removed by patch1.
        // A new instance of "A" is added by patch3 with a new tag.
        // "B" is added by patch2.
        // Final state should contain "A" and "B".
        var expected = new[] { "A", "B" };
        var firstState = finalStates.First();
        firstState.ShouldBe(expected, ignoreOrder: true);
        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState, ignoreOrder: true);
        }
    }

    [Fact]
    public void Converge_ReAddingItem_ShouldSucceed()
    {
        // Arrange
        var model = new TestModel { Tags = { "A" } };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);

        // Remove "A"
        var patchRemove = patcherA.GeneratePatch(
            document, 
            new TestModel());
        applicatorA.ApplyPatch(document, patchRemove);
        model.Tags.ShouldBeEmpty();
        
        // Add "A" again
        var patchAdd = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(new TestModel(), meta),
            new TestModel { Tags = { "A" } });
        
        // Act
        applicatorA.ApplyPatch(document, patchAdd);

        // Assert
        model.Tags.ShouldBe(new[] { "A" });
    }

    [Fact]
    public void GetStartKey_ShouldReturnSmallestKeyOrNull()
    {
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        
        strategyA.GetStartKey(new TestModel(), propInfo).ShouldBeNull();
        strategyA.GetStartKey(new TestModel { Tags = { "c", "a", "b" } }, propInfo).ShouldBe("a");
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractCorrectly()
    {
        var tsProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var op = new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("myVal", Guid.NewGuid()), tsProvider.Now(), 0);
        
        strategyA.GetKeyFromOperation(op, "$.tags").ShouldBe("myVal");
        strategyA.GetKeyFromOperation(op, "$.otherPath").ShouldBeNull();
    }

    [Fact]
    public void GetMinimumKey_ShouldReturnCorrectMinValue()
    {
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        strategyA.GetMinimumKey(propInfo).ShouldBe(string.Empty);
    }

    [Fact]
    public void Split_ShouldDivideDataAndMetadataEqually()
    {
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        var ts = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("a", Guid.NewGuid()), ts.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("b", Guid.NewGuid()), ts.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("c", Guid.NewGuid()), ts.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("d", Guid.NewGuid()), ts.Now(), 0)));

        var result = strategyA.Split(doc, meta, propInfo);

        result.SplitKey.ShouldBe("c");

        var doc1 = (TestModel)result.Partition1.Data;
        var doc2 = (TestModel)result.Partition2.Data;

        doc1.Tags.ShouldBe(["a", "b"], ignoreOrder: true);
        doc2.Tags.ShouldBe(["c", "d"], ignoreOrder: true);

        result.Partition1.Metadata.OrSets["$.tags"].Adds.Keys.ShouldContain("a");
        result.Partition2.Metadata.OrSets["$.tags"].Adds.Keys.ShouldContain("c");
    }

    [Fact]
    public void Merge_ShouldCombineDataAndMetadata()
    {
        var doc1 = new TestModel();
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel();
        var meta2 = metadataManagerA.Initialize(doc2);
        var propInfo = new OrSetStrategyTestCrdtContext().GetTypeInfo(typeof(TestModel))!.Properties[nameof(TestModel.Tags)];
        var ts = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("a", Guid.NewGuid()), ts.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("b", Guid.NewGuid()), ts.Now(), 0)));
        
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("c", Guid.NewGuid()), ts.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, new OrSetAddItem("d", Guid.NewGuid()), ts.Now(), 0)));

        var result = strategyA.Merge(doc1, meta1, doc2, meta2, propInfo);

        var mergedDoc = (TestModel)result.Data;
        mergedDoc.Tags.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
        result.Metadata.OrSets["$.tags"].Adds.Keys.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Compact_ShouldRemoveDeadItems_WhenPolicyAllows()
    {
        // Arrange
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);

        var tsProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var ts = tsProvider.Now();

        var tag1 = Guid.NewGuid();
        var tag2 = Guid.NewGuid();
        var tag3 = Guid.NewGuid();

        var adds = new Dictionary<object, ISet<Guid>>
        {
            { "alive", new HashSet<Guid> { tag3 } },
            { "dead_safe", new HashSet<Guid> { tag1 } },
            { "dead_unsafe", new HashSet<Guid> { tag2 } }
        };

        var removes = new Dictionary<object, IDictionary<Guid, CausalTimestamp>>
        {
            { "dead_safe", new Dictionary<Guid, CausalTimestamp> { { tag1, new CausalTimestamp(ts, "replica-1", 5) } } },
            { "dead_unsafe", new Dictionary<Guid, CausalTimestamp> { { tag2, new CausalTimestamp(ts, "replica-2", 10) } } }
        };

        meta.OrSets["$.tags"] = new OrSetState(adds, removes);

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "replica-1" && c.Version == 5))).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "replica-2" && c.Version == 10))).Returns(false);

        var context = new CompactionContext(meta, mockPolicy.Object, "Tags", "$.tags", doc);

        // Act
        strategyA.Compact(context);

        // Assert
        var queueState = meta.OrSets["$.tags"];
        
        queueState.Adds.ShouldContainKey("alive");
        queueState.Adds.ShouldNotContainKey("dead_safe");
        queueState.Removes.ShouldNotContainKey("dead_safe");

        queueState.Adds.ShouldContainKey("dead_unsafe");
        queueState.Removes.ShouldContainKey("dead_unsafe");
    }

    private sealed class TestTimestampProvider : ICrdtTimestampProvider
    {
        private long currentTime = 1;

        public bool IsContinuous => false;

        public ICrdtTimestamp Create(long value) => new EpochTimestamp(value);

        public ICrdtTimestamp Init()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ICrdtTimestamp> IterateBetween(ICrdtTimestamp start, ICrdtTimestamp end)
        {
            throw new NotImplementedException();
        }

        public ICrdtTimestamp Now() => new EpochTimestamp(currentTime++);
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