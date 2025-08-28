namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
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
        var patchA = patcherA.GeneratePatch(crdtDoc0, new CrdtDocument<LseqTestModel>(docA, crdtDoc0.Metadata));
        var patchB = patcherB.GeneratePatch(crdtDoc0, new CrdtDocument<LseqTestModel>(docB, crdtDoc0.Metadata));
        
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
        
        var patch = patcherA.GeneratePatch(document, new CrdtDocument<LseqTestModel>(modified, document.Metadata));

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

        var patchB = patcherA.GeneratePatch(crdtDoc0, new CrdtDocument<LseqTestModel>(new LseqTestModel { Items = ["A", "B", "D"] }, crdtDoc0.Metadata));
        var patchC = patcherB.GeneratePatch(crdtDoc0, new CrdtDocument<LseqTestModel>(new LseqTestModel { Items = ["A", "C", "D"] }, crdtDoc0.Metadata));
        var patchE = patcherC.GeneratePatch(crdtDoc0, new CrdtDocument<LseqTestModel>(new LseqTestModel { Items = ["A", "E", "D"] }, crdtDoc0.Metadata));

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

    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
}