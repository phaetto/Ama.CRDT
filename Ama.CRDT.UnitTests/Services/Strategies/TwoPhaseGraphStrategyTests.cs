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

internal sealed class TwoPhaseGraphTestModel
{
    [CrdtTwoPhaseGraphStrategy]
    public CrdtGraph Graph { get; set; } = new();
}

public sealed class TwoPhaseGraphStrategyTests : IDisposable
{
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtTimestampProvider timestampProvider;

    public TwoPhaseGraphStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<TwoPhaseGraphStrategyTestCrdtAotContext>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }

    [Fact]
    public void Vertex_CannotBeReAdded_AfterRemoval()
    {
        // Arrange
        var model = new TwoPhaseGraphTestModel();
        model.Graph.Vertices.Add("A");
        var document = new CrdtDocument<TwoPhaseGraphTestModel>(model, metadataManager.Initialize(model));

        // Act: Remove 'A'
        var patchRemove = patcherA.GeneratePatch(document, new TwoPhaseGraphTestModel());
        applicator.ApplyPatch(document, patchRemove);
        document.Data.Graph.Vertices.ShouldBeEmpty();

        // Act: Try to add 'A' back
        var emptyDoc = new CrdtDocument<TwoPhaseGraphTestModel>(new TwoPhaseGraphTestModel(), document.Metadata); // Use updated metadata
        var stateWithA = new TwoPhaseGraphTestModel();
        stateWithA.Graph.Vertices.Add("A");
        var patchAdd = patcherA.GeneratePatch(emptyDoc, stateWithA);
        applicator.ApplyPatch(document, patchAdd);

        // Assert
        document.Data.Graph.Vertices.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyPatch_WithRemoveOperations_IsIdempotent()
    {
        // Arrange
        var initialState = new TwoPhaseGraphTestModel();
        initialState.Graph.Vertices.Add("A");
        var document = new CrdtDocument<TwoPhaseGraphTestModel>(initialState, metadataManager.Initialize(initialState));

        var stateWithoutA = new TwoPhaseGraphTestModel();
        var patchRemove = patcherA.GeneratePatch(document, stateWithoutA);

        // Act
        applicator.ApplyPatch(document, patchRemove);
        document.Data.Graph.Vertices.ShouldBeEmpty();

        applicator.ApplyPatch(document, patchRemove); // Apply second time

        // Assert
        document.Data.Graph.Vertices.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyPatch_WithConcurrentAddAndRemove_IsCommutativeAndConverges()
    {
        // Arrange
        var ancestor = new TwoPhaseGraphTestModel();
        ancestor.Graph.Vertices.Add("A");
        var docAncestor = new CrdtDocument<TwoPhaseGraphTestModel>(ancestor, metadataManager.Initialize(ancestor));

        // Replica A removes vertex "A"
        var patchA = patcherA.GeneratePatch(docAncestor, new TwoPhaseGraphTestModel());

        // Replica B adds vertex "B"
        var stateB = new TwoPhaseGraphTestModel();
        stateB.Graph.Vertices.Add("A");
        stateB.Graph.Vertices.Add("B");
        var patchB = patcherB.GeneratePatch(docAncestor, stateB);

        // Act: Scenario 1 (A then B)
        var model1 = new TwoPhaseGraphTestModel { Graph = { Vertices = { "A" } } };
        var doc1 = new CrdtDocument<TwoPhaseGraphTestModel>(model1, docAncestor.Metadata.DeepClone());
        applicator.ApplyPatch(doc1, patchA);
        applicator.ApplyPatch(doc1, patchB);

        // Act: Scenario 2 (B then A)
        var model2 = new TwoPhaseGraphTestModel { Graph = { Vertices = { "A" } } };
        var doc2 = new CrdtDocument<TwoPhaseGraphTestModel>(model2, docAncestor.Metadata.DeepClone());
        applicator.ApplyPatch(doc2, patchB);
        applicator.ApplyPatch(doc2, patchA);

        // Assert
        var expectedVertices = new HashSet<object> { "B" };
        model1.Graph.Vertices.ShouldBe(expectedVertices, ignoreOrder: true);
        model2.Graph.Vertices.ShouldBe(expectedVertices, ignoreOrder: true);
    }

    [Fact]
    public void GenerateOperation_AddVertexIntent_GeneratesAndAppliesCorrectly()
    {
        // Arrange
        var model = new TwoPhaseGraphTestModel();
        var document = new CrdtDocument<TwoPhaseGraphTestModel>(model, metadataManager.Initialize(model));
        var intent = new AddVertexIntent("A");

        // Act
        var operation = patcherA.GenerateOperation(document, x => x.Graph, intent);
        applicator.ApplyPatch(document, new CrdtPatch([operation]));

        // Assert
        document.Data.Graph.Vertices.ShouldContain("A");
    }

    [Fact]
    public void GenerateOperation_RemoveVertexIntent_GeneratesAndAppliesCorrectly()
    {
        // Arrange
        var model = new TwoPhaseGraphTestModel();
        model.Graph.Vertices.Add("A");
        var document = new CrdtDocument<TwoPhaseGraphTestModel>(model, metadataManager.Initialize(model));
        var intent = new RemoveVertexIntent("A");

        // Act
        var operation = patcherA.GenerateOperation(document, x => x.Graph, intent);
        applicator.ApplyPatch(document, new CrdtPatch([operation]));

        // Assert
        document.Data.Graph.Vertices.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateOperation_AddEdgeIntent_GeneratesAndAppliesCorrectly()
    {
        // Arrange
        var model = new TwoPhaseGraphTestModel();
        model.Graph.Vertices.Add("A");
        model.Graph.Vertices.Add("B");
        var document = new CrdtDocument<TwoPhaseGraphTestModel>(model, metadataManager.Initialize(model));
        var edge = new Edge("A", "B", null);
        var intent = new AddEdgeIntent(edge);

        // Act
        var operation = patcherA.GenerateOperation(document, x => x.Graph, intent);
        applicator.ApplyPatch(document, new CrdtPatch([operation]));

        // Assert
        document.Data.Graph.Edges.ShouldContain(edge);
    }

    [Fact]
    public void GenerateOperation_RemoveEdgeIntent_GeneratesAndAppliesCorrectly()
    {
        // Arrange
        var edge = new Edge("A", "B", null);
        var model = new TwoPhaseGraphTestModel();
        model.Graph.Vertices.Add("A");
        model.Graph.Vertices.Add("B");
        model.Graph.Edges.Add(edge);
        var document = new CrdtDocument<TwoPhaseGraphTestModel>(model, metadataManager.Initialize(model));
        var intent = new RemoveEdgeIntent(edge);

        // Act
        var operation = patcherA.GenerateOperation(document, x => x.Graph, intent);
        applicator.ApplyPatch(document, new CrdtPatch([operation]));

        // Assert
        document.Data.Graph.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Compact_ShouldRemoveTombstones_WhenPolicyAllows()
    {
        // Arrange
        var strategy = scopeA.ServiceProvider.GetServices<ICrdtStrategy>().OfType<TwoPhaseGraphStrategy>().Single();
        var mockPolicy = new Mock<ICompactionPolicy>();

        // Mock policy: Safe to compact if ReplicaId == "R1" and Version <= 5
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()))
            .Returns((CompactionCandidate c) => c.ReplicaId == "R1" && c.Version <= 5);

        var metadata = new CrdtMetadata();
        var state = new TwoPhaseGraphState(
            new HashSet<object>(),
            new Dictionary<object, CausalTimestamp>(),
            new HashSet<object>(),
            new Dictionary<object, CausalTimestamp>()
        );

        state.VertexTombstones["A"] = new CausalTimestamp(timestampProvider.Create(100), "R1", 5); // Should be removed
        state.VertexTombstones["B"] = new CausalTimestamp(timestampProvider.Create(200), "R2", 10); // Should be kept

        var edgeA = new Edge("A", "B", null);
        var edgeB = new Edge("B", "C", null);
        state.EdgeTombstones[edgeA] = new CausalTimestamp(timestampProvider.Create(100), "R1", 4); // Should be removed
        state.EdgeTombstones[edgeB] = new CausalTimestamp(timestampProvider.Create(200), "R1", 6); // Should be kept

        metadata.TwoPhaseGraphs["$.graph"] = state;

        var context = new CompactionContext(metadata, mockPolicy.Object, "Graph", "$.graph", new TwoPhaseGraphTestModel());

        // Act
        strategy.Compact(context);

        // Assert
        state.VertexTombstones.ShouldNotContainKey("A");
        state.VertexTombstones.ShouldContainKey("B");

        state.EdgeTombstones.ShouldNotContainKey(edgeA);
        state.EdgeTombstones.ShouldContainKey(edgeB);
    }
}