namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

public sealed class TwoPhaseGraphStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtTwoPhaseGraphStrategy]
        public CrdtGraph Graph { get; set; } = new();
    }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtMetadataManager metadataManager;

    public TwoPhaseGraphStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
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
        var model = new TestModel();
        model.Graph.Vertices.Add("A");
        var document = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));

        // Act: Remove 'A'
        var patchRemove = patcherA.GeneratePatch(document, new TestModel());
        applicator.ApplyPatch(document, patchRemove);
        document.Data.Graph.Vertices.ShouldBeEmpty();

        // Act: Try to add 'A' back
        var emptyDoc = new CrdtDocument<TestModel>(new TestModel(), document.Metadata); // Use updated metadata
        var stateWithA = new TestModel();
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
        var initialState = new TestModel();
        initialState.Graph.Vertices.Add("A");
        var document = new CrdtDocument<TestModel>(initialState, metadataManager.Initialize(initialState));

        var stateWithoutA = new TestModel();
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
        var ancestor = new TestModel();
        ancestor.Graph.Vertices.Add("A");
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManager.Initialize(ancestor));

        // Replica A removes vertex "A"
        var patchA = patcherA.GeneratePatch(docAncestor, new TestModel());

        // Replica B adds vertex "B"
        var stateB = new TestModel();
        stateB.Graph.Vertices.Add("A");
        stateB.Graph.Vertices.Add("B");
        var patchB = patcherB.GeneratePatch(docAncestor, stateB);

        // Act: Scenario 1 (A then B)
        var model1 = new TestModel { Graph = { Vertices = { "A" } } };
        var doc1 = new CrdtDocument<TestModel>(model1, docAncestor.Metadata.DeepClone());
        applicator.ApplyPatch(doc1, patchA);
        applicator.ApplyPatch(doc1, patchB);

        // Act: Scenario 2 (B then A)
        var model2 = new TestModel { Graph = { Vertices = { "A" } } };
        var doc2 = new CrdtDocument<TestModel>(model2, docAncestor.Metadata.DeepClone());
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
        var model = new TestModel();
        var document = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));
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
        var model = new TestModel();
        model.Graph.Vertices.Add("A");
        var document = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));
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
        var model = new TestModel();
        model.Graph.Vertices.Add("A");
        model.Graph.Vertices.Add("B");
        var document = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));
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
        var model = new TestModel();
        model.Graph.Vertices.Add("A");
        model.Graph.Vertices.Add("B");
        model.Graph.Edges.Add(edge);
        var document = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));
        var intent = new RemoveEdgeIntent(edge);

        // Act
        var operation = patcherA.GenerateOperation(document, x => x.Graph, intent);
        applicator.ApplyPatch(document, new CrdtPatch([operation]));

        // Assert
        document.Data.Graph.Edges.ShouldBeEmpty();
    }
}