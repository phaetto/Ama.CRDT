namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

public sealed class LwwSetStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtLwwSetStrategy]
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
    private readonly LwwSetStrategy strategyA;
    private readonly ICrdtTimestampProvider timestampProvider;

    public LwwSetStrategyTests()
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
        strategyA = scopeA.ServiceProvider.GetRequiredService<LwwSetStrategy>();
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
    public void Converge_LastWriteWins_OnAddRemoveConflict()
    {
        // Arrange
        var ancestor = new TestModel();
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        
        var patchAdd = patcherA.GeneratePatch(docAncestor, new TestModel { Tags = { "A" } });

        Thread.Sleep(5);
        var tempDoc = new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor);
        var patchRemove = patcherB.GeneratePatch(tempDoc, new TestModel());

        // Scenario 1: Add (t=1), then Remove (t=2) -> Should be removed
        var model1 = new TestModel();
        var meta1 = metaAncestor.DeepClone();
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchAdd);
        applicatorA.ApplyPatch(doc1, patchRemove);
        
        // Scenario 2: Remove (t=2), then Add (t=1) -> Should be removed
        var model2 = new TestModel();
        var meta2 = metaAncestor.DeepClone();
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchRemove);
        applicatorA.ApplyPatch(doc2, patchAdd);

        // Assert
        model1.Tags.ShouldBeEmpty();
        model2.Tags.ShouldBeEmpty();
    }
    
    [Fact]
    public void Converge_LastWriteWins_OnRemoveAddConflict_AllowsReAdd()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);
        
        var patchRemove = patcherA.GeneratePatch(docAncestor, new TestModel());
        
        Thread.Sleep(5);
        var patchAdd = patcherB.GeneratePatch(new CrdtDocument<TestModel>(new TestModel(), metaAncestor), new TestModel { Tags = { "A" } });
        
        // Scenario 1: Remove (t=1), then Add (t=2) -> Should be present
        var model1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(model1);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchRemove);
        applicatorA.ApplyPatch(doc1, patchAdd);
        
        // Scenario 2: Add (t=2), then Remove (t=1) -> Should be present
        var model2 = new TestModel { Tags = { "A" } };
        var meta2 = metadataManagerA.Initialize(model2);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchAdd);
        applicatorA.ApplyPatch(doc2, patchRemove);

        // Assert
        model1.Tags.ShouldBe(new[] { "A" });
        model2.Tags.ShouldBe(new[] { "A" });
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
        // A is removed at t=2.
        // B is removed at t=1.
        // C is added at t=1.
        // D is added at t=2.
        // E is added at t=3.
        // Final state: C, D, E
        var expected = new[] { "C", "D", "E" };
        var firstState = finalStates.First();
        firstState.ShouldBe(expected, ignoreOrder:true);
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

        result.Partition1.Metadata.LwwSets["$.tags"].Adds.Keys.ShouldContain("a");
        result.Partition2.Metadata.LwwSets["$.tags"].Adds.Keys.ShouldContain("c");
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
        result.Metadata.LwwSets["$.tags"].Adds.Keys.Count.ShouldBeGreaterThanOrEqualTo(4);
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