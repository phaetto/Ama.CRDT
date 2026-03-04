namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
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
        var doc2Metadata = doc1.Metadata.DeepClone();
        var doc2 = new CrdtDocument<TestModel>(doc2Model, doc2Metadata!);

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new OrMapAddItem("b", 2, Guid.NewGuid()), timestampProvider.Create(1), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Remove, new OrMapRemoveItem("a", doc1.Metadata.OrMaps["$.map"].Adds["a"]), timestampProvider.Create(2), 0);

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
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new OrMapAddItem("b", 2, Guid.NewGuid()), timestampProvider.Create(1), 0);
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
        
        var removeOp = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Remove, new OrMapRemoveItem("a", doc.Metadata.OrMaps["$.map"].Adds["a"]), timestampProvider.Create(1), 0);
        var addOp = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new OrMapAddItem("a", 100, Guid.NewGuid()), timestampProvider.Create(2), 0);

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
        doc.Metadata.Lww["$.map['a']"] = timestampProvider.Create(10);
        
        var olderUpdate = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new OrMapAddItem("a", 0, Guid.NewGuid()), timestampProvider.Create(5), 0);
        var newerUpdate = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new OrMapAddItem("a", 2, Guid.NewGuid()), timestampProvider.Create(15), 0);
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, olderUpdate));
        doc.Data.Map["a"].ShouldBe(1);

        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, newerUpdate));

        // Assert
        doc.Data.Map["a"].ShouldBe(2);
    }
    
    [Fact]
    public void Split_ShouldCorrectlyDivideDataAndMetadata()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();

        var originalDoc = CreateDocument(new Dictionary<string, int>
        {
            { "a", 1 }, { "b", 2 }, { "c", 3 }, { "d", 4 }, { "e", 5 }
        });
        originalDoc.Metadata.Lww["$.map['a']"] = timestampProvider.Create(1);
        originalDoc.Metadata.Lww["$.map['d']"] = timestampProvider.Create(1);

        var propertyInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map));
        propertyInfo.ShouldNotBeNull();

        // Act
        var result = strategy.Split(originalDoc.Data, originalDoc.Metadata, propertyInfo);

        // Assert
        var doc1 = result.Partition1.Data as TestModel;
        var doc2 = result.Partition2.Data as TestModel;
        var meta1 = result.Partition1.Metadata;
        var meta2 = result.Partition2.Metadata;
        
        doc1.ShouldNotBeNull();
        doc2.ShouldNotBeNull();
        result.SplitKey.ShouldBe("c");

        doc1.Map.Keys.OrderBy(k => k).ShouldBe(new[] { "a", "b" });
        doc2.Map.Keys.OrderBy(k => k).ShouldBe(new[] { "c", "d", "e" });

        meta1.Lww.ShouldContainKey("$.map['a']");
        meta2.Lww.ShouldContainKey("$.map['d']");
        meta1.OrMaps["$.map"].Adds.Count.ShouldBe(2);
        meta2.OrMaps["$.map"].Adds.Count.ShouldBe(3);
    }
    
    [Fact]
    public void Merge_ShouldCorrectlyCombineDataAndMetadata()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 1 }, { "b", 2 } });
        doc1.Metadata.Lww["$.map['b']"] = timestampProvider.Create(10);
        
        var doc2 = CreateDocument(new Dictionary<string, int> { { "c", 3 }, { "b", 0 } }); // Conflict on "b"
        doc2.Metadata.Lww["$.map['b']"] = timestampProvider.Create(5); // Older timestamp

        var propertyInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map));
        propertyInfo.ShouldNotBeNull();

        // Act
        var result = strategy.Merge(doc1.Data, doc1.Metadata, doc2.Data, doc2.Metadata, propertyInfo);

        // Assert
        var mergedDoc = result.Data as TestModel;
        var mergedMeta = result.Metadata;
        mergedDoc.ShouldNotBeNull();

        mergedDoc.Map.Keys.OrderBy(k => k).ShouldBe(new[] { "a", "b", "c" });
        mergedDoc.Map["a"].ShouldBe(1);
        mergedDoc.Map["b"].ShouldBe(2); // From doc1, as it has the higher LWW value
        mergedDoc.Map["c"].ShouldBe(3);

        mergedMeta.OrMaps["$.map"].Adds.Count.ShouldBe(3);
        mergedMeta.Lww["$.map['b']"].ShouldBe(timestampProvider.Create(10));
    }

    [Fact]
    public void GenerateOperation_MapSetIntent_ShouldReturnUpsertOperation()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int>());
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var intent = new MapSetIntent("a", 42);
        var timestamp = timestampProvider.Create(1);
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", property, intent, timestamp, 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.map");
        operation.ReplicaId.ShouldBe("A");
        operation.Timestamp.ShouldBe(timestamp);

        var payload = operation.Value.ShouldBeOfType<OrMapAddItem>();
        payload.Key.ShouldBe("a");
        payload.Value.ShouldBe(42);
        payload.Tag.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void GenerateOperation_MapRemoveIntent_ShouldReturnRemoveOperationWithTags()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var intent = new MapRemoveIntent("a");
        var timestamp = timestampProvider.Create(2);
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", property, intent, timestamp, 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Remove);
        operation.JsonPath.ShouldBe("$.map");
        operation.ReplicaId.ShouldBe("A");
        operation.Timestamp.ShouldBe(timestamp);

        var payload = operation.Value.ShouldBeOfType<OrMapRemoveItem>();
        payload.Key.ShouldBe("a");
        var expectedTags = doc.Metadata.OrMaps["$.map"].Adds["a"];
        payload.Tags.ShouldBeSubsetOf(expectedTags);
        payload.Tags.Count.ShouldBe(expectedTags.Count);
    }

    [Fact]
    public void GenerateOperation_UnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<OrMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int>());
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var intent = new SetIntent("test");
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", property, intent, timestampProvider.Create(1), 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }

    private sealed class TestModel
    {
        [CrdtOrMapStrategy]
        public Dictionary<string, int> Map { get; set; } = [];
    }
}