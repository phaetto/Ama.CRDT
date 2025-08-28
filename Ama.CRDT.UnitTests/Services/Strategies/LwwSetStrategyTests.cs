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
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly ICrdtMetadataManager metadataManagerB;
    private readonly ICrdtMetadataManager metadataManagerC;
    private readonly ICrdtTimestampProvider timestampProviderA;
    private readonly ICrdtTimestampProvider timestampProviderB;
    private readonly ICrdtTimestampProvider timestampProviderC;

    public LwwSetStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, EpochTimestampProvider>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherC = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        metadataManagerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        metadataManagerC = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProviderA = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        timestampProviderB = scopeB.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        timestampProviderC = scopeB.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertAndRemoveOps()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A", "B" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "B", "C" } };
        
        // Act
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta1));
        
        // Assert
        patch.Operations.Count.ShouldBe(2);
        patch.Operations.ShouldContain(op => op.Type == OperationType.Remove && op.Value.ToString() == "A");
        patch.Operations.ShouldContain(op => op.Type == OperationType.Upsert && op.Value.ToString() == "C");
    }
    
    [Fact]
    public void ApplyPatch_IsTrulyIdempotent()
    {
        // Arrange
        var doc1 = new TestModel { Tags = { "A" } };
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new TestModel { Tags = { "A", "B" } };
        var patch = patcherA.GeneratePatch(new CrdtDocument<TestModel>(doc1, meta1), new CrdtDocument<TestModel>(doc2, meta1));

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
        
        var patchAdd = patcherA.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));

        Thread.Sleep(5);
        var patchRemove = patcherB.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));
        patchRemove = new CrdtPatch(new List<CrdtOperation> { patchRemove.Operations.First() with { Type = OperationType.Remove } }); // Manually create remove

        // Scenario 1: Add (t=1), then Remove (t=2) -> Should be removed
        var model1 = new TestModel();
        var meta1 = metadataManagerA.Clone(metaAncestor);
        var doc1 = new CrdtDocument<TestModel>(model1, meta1);
        applicatorA.ApplyPatch(doc1, patchAdd);
        applicatorA.ApplyPatch(doc1, patchRemove);
        
        // Scenario 2: Remove (t=2), then Add (t=1) -> Should be removed
        var model2 = new TestModel();
        var meta2 = metadataManagerA.Clone(metaAncestor);
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
        
        var patchRemove = patcherA.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel(), metaAncestor));
        
        var patchAdd = patcherB.GeneratePatch(new CrdtDocument<TestModel>(ancestor, metaAncestor), new CrdtDocument<TestModel>(new TestModel { Tags = { "A" } }, metaAncestor));
        
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
        var model1 = new TestModel { Tags = { "A", "C" } };
        var model2 = new TestModel { Tags = { "B", "D" } };
        var model3 = new TestModel { Tags = { "A", "B", "E" } };

        Thread.Sleep(5);
        var patch1 = patcherA.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(model1, metadataManagerA.Initialize(model1))); // Remove B, Add C

        Thread.Sleep(5);
        var patch2 = patcherB.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(model2, metadataManagerB.Initialize(model2))); // Remove A, Add D

        Thread.Sleep(5);
        var patch3 = patcherC.GeneratePatch(
            new CrdtDocument<TestModel>(ancestor, metaAncestor),
            new CrdtDocument<TestModel>(model3, metadataManagerC.Initialize(model3))); // Add E

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, patches.Length);
        var finalStates = new List<List<string>>();

        // Act
        foreach (var permutation in permutations)
        {
            var model = new TestModel { Tags = new List<string>(ancestor.Tags) };
            var meta = metadataManagerA.Clone(metaAncestor);
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
        firstState.ShouldBe(expected);
        foreach (var state in finalStates.Skip(1))
        {
            state.ShouldBe(firstState);
        }
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