namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public sealed class GraphStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtGraphStrategy]
        public CrdtGraph Graph { get; set; } = new();
    }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly IServiceScope scopeC;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtPatcher patcherC;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtMetadataManager metadataManager;

    public GraphStrategyTests()
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
        applicator = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
        scopeC.Dispose();
    }

    [Fact]
    public void GeneratePatch_ForNewVerticesAndEdges_CreatesUpsertOperations()
    {
        // Arrange
        var doc1 = new TestModel();
        doc1.Graph.Vertices.Add("A");
        var meta1 = metadataManager.Initialize(doc1);
        var doc2 = new TestModel();
        doc2.Graph.Vertices.Add("A");
        doc2.Graph.Vertices.Add("B");
        doc2.Graph.Edges.Add(new Edge("A", "B", "connects"));
        var document1 = new CrdtDocument<TestModel>(doc1, meta1);

        // Act
        var patch = patcherA.GeneratePatch(document1, doc2);

        // Assert
        patch.Operations.Count.ShouldBe(2);
        patch.Operations.ShouldContain(op => op.Type == OperationType.Upsert && op.Value is GraphVertexPayload && ((GraphVertexPayload)op.Value).Vertex.Equals("B"));
        patch.Operations.ShouldContain(op => op.Type == OperationType.Upsert && op.Value is GraphEdgePayload && ((GraphEdgePayload)op.Value).Edge.Equals(new Edge("A", "B", "connects")));
    }

    [Fact]
    public void ApplyPatch_WithAddOperations_IsIdempotent()
    {
        // Arrange
        var ancestor = new TestModel();
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManager.Initialize(ancestor));
        var replicaAState = new TestModel();
        replicaAState.Graph.Vertices.Add("A");
        replicaAState.Graph.Edges.Add(new Edge("X", "Y", null));
        var patch = patcherA.GeneratePatch(docAncestor, replicaAState);

        var target = new TestModel();
        var targetDocument = new CrdtDocument<TestModel>(target, metadataManager.Initialize(target));

        // Act
        applicator.ApplyPatch(targetDocument, patch);
        var verticesAfterFirstApply = new HashSet<object>(target.Graph.Vertices);
        var edgesAfterFirstApply = new HashSet<Edge>(target.Graph.Edges);

        applicator.ApplyPatch(targetDocument, patch); // Apply second time

        // Assert
        target.Graph.Vertices.ShouldBe(verticesAfterFirstApply, ignoreOrder: true);
        target.Graph.Edges.ShouldBe(edgesAfterFirstApply, ignoreOrder: true);
        target.Graph.Vertices.Count.ShouldBe(1);
        target.Graph.Edges.Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyPatch_WithConcurrentAdds_IsCommutative()
    {
        // Arrange
        var ancestor = new TestModel();
        var metadata = metadataManager.Initialize(ancestor);
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadata);

        var replicaAState = new TestModel();
        replicaAState.Graph.Vertices.Add("A");
        var patchA = patcherA.GeneratePatch(docAncestor, replicaAState);

        var replicaBState = new TestModel();
        replicaBState.Graph.Vertices.Add("B");
        var patchB = patcherB.GeneratePatch(docAncestor, replicaBState);

        // Act: Scenario 1 (A then B)
        var model1 = new TestModel();
        var doc1 = new CrdtDocument<TestModel>(model1, metadataManager.Clone(metadata));
        applicator.ApplyPatch(doc1, patchA);
        applicator.ApplyPatch(doc1, patchB);

        // Act: Scenario 2 (B then A)
        var model2 = new TestModel();
        var doc2 = new CrdtDocument<TestModel>(model2, metadataManager.Clone(metadata));
        applicator.ApplyPatch(doc2, patchB);
        applicator.ApplyPatch(doc2, patchA);

        // Assert
        var expectedVertices = new HashSet<object> { "A", "B" };
        model1.Graph.Vertices.ShouldBe(expectedVertices, ignoreOrder: true);
        model2.Graph.Vertices.ShouldBe(expectedVertices, ignoreOrder: true);
    }

    [Fact]
    public void ApplyPatch_WithConcurrentAdds_IsAssociative()
    {
        // Arrange
        var ancestor = new TestModel();
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManager.Initialize(ancestor));

        var patchA = patcherA.GeneratePatch(docAncestor, new TestModel { Graph = { Vertices = { "A" } } });
        var patchB = patcherB.GeneratePatch(docAncestor, new TestModel { Graph = { Vertices = { "B" } } });
        var patchC = patcherC.GeneratePatch(docAncestor, new TestModel { Graph = { Vertices = { "C" } } });

        // Act: Scenario 1 ((A + B) + C)
        var model1 = new TestModel();
        var doc1 = new CrdtDocument<TestModel>(model1, metadataManager.Initialize(model1));
        applicator.ApplyPatch(doc1, patchA);
        applicator.ApplyPatch(doc1, patchB);
        applicator.ApplyPatch(doc1, patchC);

        // Act: Scenario 2 (A + (B + C))
        var model2 = new TestModel();
        var doc2 = new CrdtDocument<TestModel>(model2, metadataManager.Initialize(model2));
        applicator.ApplyPatch(doc2, patchB);
        applicator.ApplyPatch(doc2, patchC);
        applicator.ApplyPatch(doc2, patchA);

        // Assert
        var expectedVertices = new HashSet<object> { "A", "B", "C" };
        model1.Graph.Vertices.ShouldBe(expectedVertices, ignoreOrder: true);
        model2.Graph.Vertices.ShouldBe(expectedVertices, ignoreOrder: true);
    }
}