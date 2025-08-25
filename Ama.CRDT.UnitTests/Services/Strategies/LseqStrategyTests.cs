namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class LseqStrategyTests
{
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtMetadataManager metadataManager;

    public LseqStrategyTests()
    {
        var services = new ServiceCollection()
            .AddCrdt(options => options.ReplicaId = "A")
            .BuildServiceProvider();

        var factory = services.GetRequiredService<ICrdtPatcherFactory>();
        patcherA = factory.Create("A");
        patcherB = factory.Create("B");
        patcherC = factory.Create("C");
        applicator = services.GetRequiredService<ICrdtApplicator>();
        metadataManager = services.GetRequiredService<ICrdtMetadataManager>();
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
        var meta0 = metadataManager.Initialize(doc0);
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
            metadataManager.Initialize(new LseqTestModel { Items = ["A", "C"] })
        );
        applicator.ApplyPatch(doc1.Data, patchA, doc1.Metadata);
        applicator.ApplyPatch(doc1.Data, patchB, doc1.Metadata);

        // Path 2: Apply B then A
        var doc2 = new CrdtDocument<LseqTestModel>(
            new LseqTestModel { Items = ["A", "C"] },
            metadataManager.Initialize(new LseqTestModel { Items = ["A", "C"] })
        );
        applicator.ApplyPatch(doc2.Data, patchB, doc2.Metadata);
        applicator.ApplyPatch(doc2.Data, patchA, doc2.Metadata);

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
        var document = new CrdtDocument<LseqTestModel>(original, metadataManager.Initialize(original));
        
        var patch = patcherA.GeneratePatch(document, new CrdtDocument<LseqTestModel>(modified, document.Metadata));

        // Act
        applicator.ApplyPatch(document.Data, patch, document.Metadata);
        var stateAfterFirstApply = document.Data.Items.ToList();
        
        // Clear seen exceptions to test the strategy's own idempotency
        document.Metadata.SeenExceptions.Clear();
        applicator.ApplyPatch(document.Data, patch, document.Metadata);

        // Assert
        stateAfterFirstApply.ShouldBe(new List<string> { "A", "C", "B" });
        document.Data.Items.ShouldBe(stateAfterFirstApply);
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentOps_ShouldBeAssociative()
    {
        // Arrange
        var doc0 = new LseqTestModel { Items = ["A", "D"] };
        var crdtDoc0 = new CrdtDocument<LseqTestModel>(doc0, metadataManager.Initialize(doc0));

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
            var meta = metadataManager.Initialize(model);
            foreach (var patch in p)
            {
                applicator.ApplyPatch(model, patch, meta);
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