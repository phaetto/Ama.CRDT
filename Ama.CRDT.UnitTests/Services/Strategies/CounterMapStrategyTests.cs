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

public sealed class CounterMapStrategyTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<IElementComparerProvider> comparerProviderMock = new();
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtScopeFactory scopeFactory;

    public CounterMapStrategyTests()
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
    public void GenerateOperation_ShouldCreateIncrementOperation_WhenUsingMapIncrementIntent()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var metadata = new CrdtMetadata();
        var intent = new MapIncrementIntent("a", 5);
        var context = new GenerateOperationContext(new TestModel(), metadata, "$.map", propInfo, intent, timestampProvider.Now(), 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Increment);
        operation.JsonPath.ShouldBe("$.map");
        operation.ReplicaId.ShouldBe("A");
        var payload = (KeyValuePair<object, object?>)operation.Value!;
        payload.Key.ShouldBe("a");
        payload.Value.ShouldBe(5);
    }

    [Fact]
    public void GenerateOperation_ShouldThrowNotSupportedException_ForOtherIntents()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var metadata = new CrdtMetadata();
        var intent = new SetIntent(5);
        var context = new GenerateOperationContext(new TestModel(), metadata, "$.map", propInfo, intent, timestampProvider.Now(), 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_Commutative_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now(), 1);
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("b", 2), timestampProvider.Now(), 1);
        var op3 = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", -3), timestampProvider.Now(), 1);
        
        // Act: Apply op1, op2, op3
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op2));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op3));

        // Act: Apply op3, op1, op2
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op3));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op2));

        // Assert
        doc1.Data.Map.ShouldBe(doc2.Data.Map);
        doc1.Data.Map["a"].ShouldBe(12); // 10 + 5 - 3
        doc1.Data.Map["b"].ShouldBe(2);
    }

    [Fact]
    public void ApplyOperation_Associative_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now(), 1);
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("b", 2), timestampProvider.Now(), 1);
        var op3 = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", -3), timestampProvider.Now(), 1);

        // Act: Apply (op1 + op2) + op3
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op2));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, op3));

        // Act: Apply op1 + (op2 + op3) by changing application order
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op2));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op3));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, op1));

        // Assert
        doc1.Data.Map.ShouldBe(doc2.Data.Map);
        doc1.Data.Map["a"].ShouldBe(12); // 10 + 5 - 3
        doc1.Data.Map["b"].ShouldBe(2);
    }

    [Fact]
    public void ApplyOperation_Idempotency_ShouldNotChangeStateWhenOperationIsFiltered()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now(), 1);

        // Act
        // Simulate the CrdtApplicator's role of filtering seen operations.
        // First time, the operation is new.
        applicator.ApplyPatch(doc, new CrdtPatch { Operations = [ op ] });

        var stateAfterFirstApply = new Dictionary<string, int>(doc.Data.Map);
        var metadataCountersAfterFirstApply = new Dictionary<string, IDictionary<object, PnCounterState>>(doc.Metadata.CounterMaps);

        // Second time, the operation has been seen and should be ignored by the applicator.
        applicator.ApplyPatch(doc, new CrdtPatch { Operations = [op] });

        // Assert
        doc.Data.Map.ShouldBe(stateAfterFirstApply);
        doc.Metadata.CounterMaps.ShouldBe(metadataCountersAfterFirstApply);
        doc.Data.Map["a"].ShouldBe(15);
    }

    [Fact]
    public void GeneratePatch_ShouldCreateCorrectDelta()
    {
        // Arrange
        using var scopeA = scopeFactory.CreateScope("A");
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 10 }, { "b", 5 } });
        var modifiedModel = new TestModel { Map = new Dictionary<string, int> { { "a", 12 }, { "c", 3 } } }; // a changed, b removed, c added

        // Act
        var patch = patcherA.GeneratePatch(doc, modifiedModel);

        // Assert
        patch.Operations.Count.ShouldBe(3);
        
        var opA = patch.Operations.Single(o => ((KeyValuePair<object, object?>)o.Value!).Key.ToString() == "a");
        opA.Type.ShouldBe(OperationType.Increment);
        Convert.ToDecimal(((KeyValuePair<object, object?>)opA.Value!).Value).ShouldBe(2);

        var opB = patch.Operations.Single(o => ((KeyValuePair<object, object?>)o.Value!).Key.ToString() == "b");
        opB.Type.ShouldBe(OperationType.Increment);
        Convert.ToDecimal(((KeyValuePair<object, object?>)opB.Value!).Value).ShouldBe(-5);

        var opC = patch.Operations.Single(o => ((KeyValuePair<object, object?>)o.Value!).Key.ToString() == "c");
        opC.Type.ShouldBe(OperationType.Increment);
        Convert.ToDecimal(((KeyValuePair<object, object?>)opC.Value!).Value).ShouldBe(3);
    }

    [Fact]
    public void GetStartKey_ShouldReturnSmallestKeyOrNull()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;

        // Act & Assert
        strategy.GetStartKey(new TestModel(), propInfo).ShouldBeNull();
        strategy.GetStartKey(new TestModel { Map = { ["c"] = 1, ["a"] = 2, ["b"] = 3 } }, propInfo).ShouldBe("a");
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractKeyCorrectly()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("myKey", 5), timestampProvider.Now(), 1);

        // Act & Assert
        strategy.GetKeyFromOperation(op, "$.map").ShouldBe("myKey");
        strategy.GetKeyFromOperation(op, "$.otherPath").ShouldBeNull();
    }

    [Fact]
    public void GetMinimumKey_ShouldReturnCorrectMinValue()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        var stringMapProp = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;
        var intMapProp = typeof(TestModelInt).GetProperty(nameof(TestModelInt.Map))!;

        // Act & Assert
        strategy.GetMinimumKey(stringMapProp).ShouldBe(string.Empty);
        strategy.GetMinimumKey(intMapProp).ShouldBe(int.MinValue);
    }

    [Fact]
    public void Split_ShouldDivideDataAndMetadataEqually()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;

        var doc = CreateDocument(new Dictionary<string, int>());
        
        // Setup initial state via ApplyOperation to correctly populate metadata
        var operations = new[]
        {
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 10), timestampProvider.Now(), 0),
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("b", 20), timestampProvider.Now(), 0),
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("c", 30), timestampProvider.Now(), 0),
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("d", 40), timestampProvider.Now(), 0)
        };

        foreach (var op in operations)
        {
            strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));
        }

        // Act
        var result = strategy.Split(doc.Data, doc.Metadata, propInfo);

        // Assert
        result.SplitKey.ShouldBe("c");

        var doc1 = (TestModel)result.Partition1.Data;
        var doc2 = (TestModel)result.Partition2.Data;

        doc1.Map.Keys.ShouldBe(["a", "b"], ignoreOrder: true);
        doc1.Map["a"].ShouldBe(10);
        doc1.Map["b"].ShouldBe(20);

        doc2.Map.Keys.ShouldBe(["c", "d"], ignoreOrder: true);
        doc2.Map["c"].ShouldBe(30);
        doc2.Map["d"].ShouldBe(40);

        result.Partition1.Metadata.CounterMaps["$.map"].Keys.ShouldBe(["a", "b"], ignoreOrder: true);
        result.Partition2.Metadata.CounterMaps["$.map"].Keys.ShouldBe(["c", "d"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_ShouldCombineDataAndMetadata()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        var propInfo = typeof(TestModel).GetProperty(nameof(TestModel.Map))!;

        var doc1 = CreateDocument(new Dictionary<string, int>());
        var doc2 = CreateDocument(new Dictionary<string, int>());

        // Setup partition 1
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, 
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 10), timestampProvider.Now(), 0)));
        strategy.ApplyOperation(new ApplyOperationContext(doc1.Data, doc1.Metadata, 
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("b", 20), timestampProvider.Now(), 0)));

        // Setup partition 2
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, 
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("c", 30), timestampProvider.Now(), 0)));
        strategy.ApplyOperation(new ApplyOperationContext(doc2.Data, doc2.Metadata, 
            new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("d", 40), timestampProvider.Now(), 0)));

        // Act
        var merged = strategy.Merge(doc1.Data, doc1.Metadata, doc2.Data, doc2.Metadata, propInfo);

        // Assert
        var mergedDoc = (TestModel)merged.Data;
        mergedDoc.Map.Keys.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
        mergedDoc.Map["a"].ShouldBe(10);
        mergedDoc.Map["b"].ShouldBe(20);
        mergedDoc.Map["c"].ShouldBe(30);
        mergedDoc.Map["d"].ShouldBe(40);

        merged.Metadata.CounterMaps["$.map"].Keys.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
    }

    private sealed class TestModel
    {
        [CrdtCounterMapStrategy]
        public Dictionary<string, int> Map { get; set; } = [];
    }

    private sealed class TestModelInt
    {
        [CrdtCounterMapStrategy]
        public Dictionary<int, int> Map { get; set; } = [];
    }
}