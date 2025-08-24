namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Generic;
using System.Linq;

public sealed class LseqStrategyTests
{
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
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
        var crdtDoc0 = new CrdtDocument<LseqTestModel>(doc0, metadataManager.Initialize(doc0));

        var docA = new LseqTestModel { Items = ["A", "B", "C"] };
        var docB = new LseqTestModel { Items = ["A", "D", "C"] };

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
    public void ApplyPatch_WithIdempotency_ShouldNotChangeStateOnSecondApply()
    {
        // Arrange
        var original = new LseqTestModel { Items = ["A", "B"] };
        var modified = new LseqTestModel { Items = ["A", "C", "B"] };
        var document = new CrdtDocument<LseqTestModel>(original, metadataManager.Initialize(original));
        
        var patch = patcherA.GeneratePatch(document, new CrdtDocument<LseqTestModel>(modified, document.Metadata));

        // Act
        applicator.ApplyPatch(document.Data, patch, document.Metadata);
        var stateAfterFirstApply = document.Data.Items.ToList();

        applicator.ApplyPatch(document.Data, patch, document.Metadata);

        // Assert
        stateAfterFirstApply.ShouldBe(new List<string> { "A", "C", "B" });
        document.Data.Items.ShouldBe(stateAfterFirstApply);
    }
}