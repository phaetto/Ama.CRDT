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
using System.Text.Json;
using Xunit;

public sealed class OrMapStrategyTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<IElementComparerProvider> comparerProviderMock = new();
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtScopeFactory scopeFactory;

    public OrMapStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
            .AddSingleton(comparerProviderMock.Object);
        
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
    public void ApplyOperation_Commutativity_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        var doc2Model = new TestModel { Map = new Dictionary<string, int>(doc1.Data.Map) };
        var doc2Metadata = JsonSerializer.Deserialize<CrdtMetadata>(JsonSerializer.Serialize(doc1.Metadata));
        var doc2 = new CrdtDocument<TestModel>(doc2Model, doc2Metadata!);

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new OrMapAddItem("b", 2, Guid.NewGuid()), timestampProvider.Create(1));
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Remove, new OrMapRemoveItem("a", doc1.Metadata.OrMaps["$.map"].Adds["a"]), timestampProvider.Create(2));

        // Act: Apply op1 then op2
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op2));

        // Act: Apply op2 then op1
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op2));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op1));

        // Assert
        doc1.Data.Map.ShouldBe(doc2.Data.Map);
        doc1.Data.Map.ShouldNotContainKey("a");
        doc1.Data.Map.ShouldContainKey("b");
        doc1.Data.Map["b"].ShouldBe(2);
    }

    [Fact]
    public void ApplyOperation_Idempotency_ShouldNotChangeState()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new OrMapAddItem("b", 2, Guid.NewGuid()), timestampProvider.Create(1));
        var context = new ApplyOperationContext(doc.Data, doc.Metadata, op);

        // Act
        strategy.ApplyOperation(context);
        var mapAfterFirstApply = new Dictionary<string, int>(doc.Data.Map);
        strategy.ApplyOperation(context);
        strategy.ApplyOperation(context);
        
        // Assert
        doc.Data.Map.ShouldBe(mapAfterFirstApply);
        doc.Data.Map.Count.ShouldBe(2);
    }

    [Fact]
    public void ApplyOperation_RemoveAndReAdd_ShouldSucceed()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        
        var removeOp = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Remove, new OrMapRemoveItem("a", doc.Metadata.OrMaps["$.map"].Adds["a"]), timestampProvider.Create(1));
        var addOp = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new OrMapAddItem("a", 100, Guid.NewGuid()), timestampProvider.Create(2));

        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, removeOp));
        doc.Data.Map.ShouldBeEmpty();

        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, addOp));

        // Assert
        doc.Data.Map.ShouldContainKey("a");
        doc.Data.Map["a"].ShouldBe(100);
    }
    
    [Fact]
    public void ApplyOperation_ValueUpdate_ShouldRespectLww()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        doc.Metadata.Lww["$.map.['a']"] = timestampProvider.Create(10);
        
        var olderUpdate = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new OrMapAddItem("a", 0, Guid.NewGuid()), timestampProvider.Create(5));
        var newerUpdate = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new OrMapAddItem("a", 2, Guid.NewGuid()), timestampProvider.Create(15));
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, olderUpdate));
        doc.Data.Map["a"].ShouldBe(1);

        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, newerUpdate));

        // Assert
        doc.Data.Map["a"].ShouldBe(2);
    }

    private sealed class TestModel
    {
        [CrdtOrMapStrategy]
        public Dictionary<string, int> Map { get; set; } = [];
    }
}