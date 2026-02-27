namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

public sealed class GSetStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtGSetStrategy]
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
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly GSetStrategy strategyA;

    public GSetStrategyTests()
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
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        strategyA = scopeA.ServiceProvider.GetRequiredService<GSetStrategy>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
        scopeC.Dispose();
    }

    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForAddedItem()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A", "B" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), doc2);
        
        // Assert
        patch.Operations.ShouldHaveSingleItem();
        var op = patch.Operations.First();
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.tags");
        op.Value.ShouldBe("B");
    }

    [Fact]
    public void GeneratePatch_ShouldNotCreateOpForRemovedItem()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), doc2);
        
        // Assert
        patch.Operations.ShouldBeEmpty();
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
        
        // Clear SeenExceptions to prove the strategy logic itself is idempotent
        targetMeta.SeenExceptions.Clear();
        applicatorA.ApplyPatch(targetDocument, patch);
        
        // Assert
        target.Tags.ShouldBe(stateAfterFirst);
        target.Tags.ShouldBe(new[] { "A", "B" }, ignoreOrder: true);
    }
    
    [Fact]
    public void ApplyPatch_ShouldIgnoreRemoveOperations()
    {
        // Arrange
        var doc = new TestModel { Tags = { "A", "B" } };
        var meta = metadataManagerA.Initialize(doc);
        var document = new CrdtDocument<TestModel>(doc, meta);
        var removeOp = new CrdtOperation(System.Guid.NewGuid(), "A", "$.tags", OperationType.Remove, "A", timestampProvider.Create(1));
        var patch = new CrdtPatch(new List<CrdtOperation> { removeOp });

        // Act
        applicatorA.ApplyPatch(document, patch);

        // Assert
        doc.Tags.ShouldBe(new[] { "A", "B" });
    }

    [Fact]
    public void Converge_WhenApplyingConcurrentAdds_ShouldBeCommutative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);

        var patchA = patcherA.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "B" } });

        var patchB = patcherB.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "C" } });
        
        // Scenario 1: A then B
        var model1 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta1 = metadataManagerA.Initialize(model1);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchA);
        applicatorA.ApplyPatch(doc1, patchB);

        // Scenario 2: B then A
        var model2 = new TestModel { Tags = new List<string>(ancestor.Tags) };
        var meta2 = metadataManagerA.Initialize(model2);
        var doc2 = new CrdtDocument<TestModel>(model2, meta2);
        applicatorA.ApplyPatch(doc2, patchB);
        applicatorA.ApplyPatch(doc2, patchA);

        // Assert
        var expected = new[] { "A", "B", "C" };
        model1.Tags.ShouldBe(expected, ignoreOrder: true);
        model2.Tags.ShouldBe(expected, ignoreOrder: true);
    }
    
    [Fact]
    public void Converge_WhenApplyingConcurrentAdds_ShouldBeAssociative()
    {
        // Arrange
        var ancestor = new TestModel { Tags = { "A" } };
        var metaAncestor = metadataManagerA.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metaAncestor);

        var patch1 = patcherA.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "B" } });

        var patch2 = patcherB.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "C" } });
        
        var patch3 = patcherC.GeneratePatch(
            docAncestor,
            new TestModel { Tags = { "A", "D" } });

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
        var expected = new[] { "A", "B", "C", "D" };
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
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;
        
        strategyA.GetStartKey(new TestModel(), propInfo).ShouldBeNull();
        strategyA.GetStartKey(new TestModel { Tags = { "c", "a", "b" } }, propInfo).ShouldBe("a");
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractCorrectly()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "myVal", timestampProvider.Now());
        
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
    public void Split_ShouldDivideDataEqually()
    {
        var doc = new TestModel();
        var meta = metadataManagerA.Initialize(doc);
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;

        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "a", timestampProvider.Now())));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "b", timestampProvider.Now())));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "c", timestampProvider.Now())));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "d", timestampProvider.Now())));

        var result = strategyA.Split(doc, meta, propInfo);

        result.SplitKey.ShouldBe("c");

        var doc1 = (TestModel)result.Partition1.Data;
        var doc2 = (TestModel)result.Partition2.Data;

        doc1.Tags.ShouldBe(["a", "b"], ignoreOrder: true);
        doc2.Tags.ShouldBe(["c", "d"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_ShouldCombineData()
    {
        var doc1 = new TestModel();
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel();
        var meta2 = metadataManagerA.Initialize(doc2);
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Tags))!;

        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "a", timestampProvider.Now())));
        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "b", timestampProvider.Now())));
        
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "c", timestampProvider.Now())));
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.tags", OperationType.Upsert, "d", timestampProvider.Now())));

        var result = strategyA.Merge(doc1, meta1, doc2, meta2, propInfo);

        var mergedDoc = (TestModel)result.Data;
        mergedDoc.Tags.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
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