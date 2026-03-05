namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
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

public sealed class MinWinsMapStrategyTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<IElementComparerProvider> comparerProviderMock = new();
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtScopeFactory scopeFactory;

    public MinWinsMapStrategyTests()
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
    public void ApplyOperation_Commutative_ShouldConvergeToMinValue()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });

        var op_lower = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now(), 0);
        var op_higher = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 15), timestampProvider.Now(), 0);
        var op_newkey = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 100), timestampProvider.Now(), 0);

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
        doc1.Data.Map["a"].ShouldBe(5); // Converged to min value
        doc1.Data.Map["b"].ShouldBe(100);
    }

    [Fact]
    public void ApplyOperation_Associative_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 100 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 100 } });

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 50), timestampProvider.Now(), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 25), timestampProvider.Now(), 0);
        var op3 = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 10), timestampProvider.Now(), 0);

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
        doc1.Data.Map["a"].ShouldBe(25); // min(100, 50, 25)
        doc1.Data.Map["b"].ShouldBe(10);
    }

    [Fact]
    public void ApplyOperation_Idempotency_ShouldNotChangeState()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 2), timestampProvider.Now(), 0);
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));
        var stateAfterFirstApply = new Dictionary<string, int>(doc.Data.Map);

        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));

        // Assert
        doc.Data.Map.ShouldBe(stateAfterFirstApply);
        doc.Data.Map["a"].ShouldBe(2);
    }
    
    [Fact]
    public void GeneratePatch_ShouldNotCreatePatchForHigherValueOrRemoval()
    {
        // Arrange
        using var scopeA = scopeFactory.CreateScope("A");
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 100 }, { "b", 50 } });
        var modifiedModel = new TestModel { Map = new Dictionary<string, int> { { "a", 110 }, { "c", 20 } } }; // a increased, b removed, c added

        // Act
        var patch = patcherA.GeneratePatch(doc, modifiedModel);

        // Assert
        patch.Operations.Count.ShouldBe(1);
        
        var opC = patch.Operations.Single();
        opC.Type.ShouldBe(OperationType.Upsert);
        ((KeyValuePair<object, object?>)opC.Value!).Key.ShouldBe("c");
        ((KeyValuePair<object, object?>)opC.Value!).Value.ShouldBe(20);
    }

    [Fact]
    public void GetStartKey_ShouldReturnSmallestKeyOrNull()
    {
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;

        strategy.GetStartKey(new TestModel(), propInfo).ShouldBeNull();
        strategy.GetStartKey(new TestModel { Map = { ["c"] = 1, ["a"] = 2, ["b"] = 3 } }, propInfo).ShouldBe("a");
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractKeyCorrectly()
    {
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("myKey", 5), timestampProvider.Now(), 1);

        strategy.GetKeyFromOperation(op, "$.map").ShouldBe("myKey");
        strategy.GetKeyFromOperation(op, "$.otherPath").ShouldBeNull();
    }

    [Fact]
    public void GetMinimumKey_ShouldReturnCorrectMinValue()
    {
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();
        var stringMapProp = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;

        strategy.GetMinimumKey(stringMapProp).ShouldBe(string.Empty);
    }

    [Fact]
    public void Split_ShouldDivideDataEqually()
    {
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;

        var doc = CreateDocument(new Dictionary<string, int>());
        
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 10), timestampProvider.Now(), 0)));
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 20), timestampProvider.Now(), 0)));
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("c", 30), timestampProvider.Now(), 0)));
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("d", 40), timestampProvider.Now(), 0)));

        var result = strategy.Split(doc.Data, doc.Metadata, propInfo);

        result.SplitKey.ShouldBe("c");

        var doc1 = (TestModel)result.Partition1.Data;
        var doc2 = (TestModel)result.Partition2.Data;

        doc1.Map.Keys.ShouldBe(["a", "b"], ignoreOrder: true);
        doc2.Map.Keys.ShouldBe(["c", "d"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_ShouldCombineDataAndResolveConflicts()
    {
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;

        var doc1 = CreateDocument(new Dictionary<string, int>());
        var doc2 = CreateDocument(new Dictionary<string, int>());

        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 10), timestampProvider.Now(), 0)));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 20), timestampProvider.Now(), 0)));

        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 5), timestampProvider.Now(), 0))); // lower value
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("c", 30), timestampProvider.Now(), 0)));

        var merged = strategy.Merge(doc1.Data, doc1.Metadata, doc2.Data, doc2.Metadata, propInfo);

        var mergedDoc = (TestModel)merged.Data;
        mergedDoc.Map.Keys.ShouldBe(["a", "b", "c"], ignoreOrder: true);
        mergedDoc.Map["a"].ShouldBe(10);
        mergedDoc.Map["b"].ShouldBe(5); // Min won
        mergedDoc.Map["c"].ShouldBe(30);
    }

    [Fact]
    public void GenerateOperation_MapSetIntent_ShouldReturnUpsertOperation()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var doc = CreateDocument(new Dictionary<string, int>());
        var timestamp = timestampProvider.Now();
        var intent = new MapSetIntent("a", 15);
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", propInfo, intent, timestamp, 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.map");
        operation.ReplicaId.ShouldBe("A");
        operation.Timestamp.ShouldBe(timestamp);
        var payload = (KeyValuePair<object, object?>)operation.Value!;
        payload.Key.ShouldBe("a");
        payload.Value.ShouldBe(15);
    }

    [Fact]
    public void GenerateOperation_UnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var doc = CreateDocument(new Dictionary<string, int>());
        var timestamp = timestampProvider.Now();
        var intent = new MapRemoveIntent("a");
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", propInfo, intent, timestamp, 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }

    private sealed class TestModel
    {
        [CrdtMinWinsMapStrategy]
        public Dictionary<string, int> Map { get; set; } = [];
    }
}