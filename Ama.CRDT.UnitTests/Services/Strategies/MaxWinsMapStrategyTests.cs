namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class MaxWinsMapStrategyTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<IElementComparerProvider> comparerProviderMock = new();
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtScopeFactory scopeFactory;

    public MaxWinsMapStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
            .AddSingleton(comparerProviderMock.Object)
            .AddCrdtTimestampProvider<EpochTimestampProvider>();

        serviceProvider = services.BuildServiceProvider();
        scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        
        using var scope = scopeFactory.CreateScope("test");
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        comparerProviderMock.Setup(p => p.GetComparer(It.IsAny<Type>())).Returns(EqualityComparer<object>.Default);
    }

    private CrdtDocument<TestModel> CreateDocument(Dictionary<string, int> map)
    {
        var model = new TestModel { Map = map };
        var metadata = metadataManager.Initialize(model);
        return new CrdtDocument<TestModel>(model, metadata);
    }

    [Fact]
    public void ApplyOperation_Commutative_ShouldConvergeToMaxValue()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });

        var op_lower = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now());
        var op_higher = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 15), timestampProvider.Now());
        var op_newkey = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 100), timestampProvider.Now());

        // Act: Apply lower, higher, newkey
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op_lower));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op_higher));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op_newkey));

        // Act: Apply newkey, higher, lower
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op_newkey));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op_higher));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op_lower));

        // Assert
        doc1.Data.Map.ShouldBe(doc2.Data.Map);
        doc1.Data.Map["a"].ShouldBe(15); // Converged to max value
        doc1.Data.Map["b"].ShouldBe(100);
    }

    [Fact]
    public void ApplyOperation_Associative_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 50), timestampProvider.Now());
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 25), timestampProvider.Now());
        var op3 = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 100), timestampProvider.Now());

        // Act: Apply (op1 + op2) + op3
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op2));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op3));

        // Act: Apply op1 + (op2 + op3) by changing application order
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op3));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op2));

        // Assert
        doc1.Data.Map.ShouldBe(doc2.Data.Map);
        doc1.Data.Map["a"].ShouldBe(50); // max(10, 50, 25)
        doc1.Data.Map["b"].ShouldBe(100);
    }

    [Fact]
    public void ApplyOperation_Idempotency_ShouldNotChangeState()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsMapStrategy>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 20), timestampProvider.Now());
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));
        var stateAfterFirstApply = new Dictionary<string, int>(doc.Data.Map);

        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));

        // Assert
        doc.Data.Map.ShouldBe(stateAfterFirstApply);
        doc.Data.Map["a"].ShouldBe(20);
    }
    
    [Fact]
    public void GeneratePatch_ShouldNotCreatePatchForLowerValueOrRemoval()
    {
        // Arrange
        using var scopeA = scopeFactory.CreateScope("A");
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 100 }, { "b", 50 } });
        var modifiedModel = new TestModel { Map = new Dictionary<string, int> { { "a", 90 }, { "c", 200 } } }; // a decreased, b removed, c added

        // Act
        var patch = patcherA.GeneratePatch(doc, modifiedModel);

        // Assert
        patch.Operations.Count.ShouldBe(1);
        
        var opC = patch.Operations.Single();
        opC.Type.ShouldBe(OperationType.Upsert);
        ((KeyValuePair<object, object?>)opC.Value!).Key.ShouldBe("c");
        ((KeyValuePair<object, object?>)opC.Value!).Value.ShouldBe(200);
    }

    private sealed class TestModel
    {
        [CrdtMaxWinsMapStrategy]
        public Dictionary<string, int> Map { get; set; } = [];
    }
}