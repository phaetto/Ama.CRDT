namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
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

[CrdtAotType(typeof(CounterMapTestModel))]
[CrdtAotType(typeof(CounterMapTestModelInt))]
[CrdtAotType(typeof(Dictionary<string, int>))]
[CrdtAotType(typeof(Dictionary<int, int>))]
internal partial class CounterMapStrategyTestCrdtAotContext : CrdtAotContext
{
}

internal sealed class CounterMapTestModel
{
    [CrdtCounterMapStrategy]
    public Dictionary<string, int> Map { get; set; } = [];
}

internal sealed class CounterMapTestModelInt
{
    [CrdtCounterMapStrategy]
    public Dictionary<int, int> Map { get; set; } = [];
}

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
            .AddCrdtAotContext<CounterMapStrategyTestCrdtAotContext>()
            .AddSingleton(comparerProviderMock.Object);

        serviceProvider = services.BuildServiceProvider();
        scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        
        using var scope = scopeFactory.CreateScope("test");
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        comparerProviderMock.Setup(p => p.GetComparer(It.IsAny<Type>())).Returns(EqualityComparer<object>.Default);
    }

    private CrdtDocument<CounterMapTestModel> CreateDocument(Dictionary<string, int> map)
    {
        var model = new CounterMapTestModel { Map = map };
        var metadata = metadataManager.Initialize(model);
        return new CrdtDocument<CounterMapTestModel>(model, metadata);
    }

    [Fact]
    public void GenerateOperation_ShouldCreateIncrementOperation_WhenUsingMapIncrementIntent()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        
        var propInfo = new CrdtPropertyInfo(
            "Map",
            "map",
            typeof(Dictionary<string, int>),
            true,
            true,
            obj => ((CounterMapTestModel)obj).Map,
            (obj, val) => ((CounterMapTestModel)obj).Map = (Dictionary<string, int>)val!,
            new CrdtCounterMapStrategyAttribute(),
            []);
            
        var metadata = new CrdtMetadata();
        var intent = new MapIncrementIntent("a", 5);
        var context = new GenerateOperationContext(new CounterMapTestModel(), metadata, "$.map", propInfo, intent, timestampProvider.Now(), 0);

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
        
        var propInfo = new CrdtPropertyInfo(
            "Map",
            "map",
            typeof(Dictionary<string, int>),
            true,
            true,
            obj => ((CounterMapTestModel)obj).Map,
            (obj, val) => ((CounterMapTestModel)obj).Map = (Dictionary<string, int>)val!,
            new CrdtCounterMapStrategyAttribute(),
            []);
            
        var metadata = new CrdtMetadata();
        var intent = new SetIntent(5);
        var context = new GenerateOperationContext(new CounterMapTestModel(), metadata, "$.map", propInfo, intent, timestampProvider.Now(), 0);

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
        var metadataCountersAfterFirstApply = ((CounterMapState)doc.Metadata.States["$.map"]).Keys.ToDictionary(k => k.Key, v => v.Value);

        // Second time, the operation has been seen and should be ignored by the applicator.
        applicator.ApplyPatch(doc, new CrdtPatch { Operations = [op] });

        // Assert
        doc.Data.Map.ShouldBe(stateAfterFirstApply);
        ((CounterMapState)doc.Metadata.States["$.map"]).Keys.ShouldBe(metadataCountersAfterFirstApply);
        doc.Data.Map["a"].ShouldBe(15);
    }

    [Fact]
    public void GeneratePatch_ShouldCreateCorrectDelta()
    {
        // Arrange
        using var scopeA = scopeFactory.CreateScope("A");
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 10 }, { "b", 5 } });
        var modifiedModel = new CounterMapTestModel { Map = new Dictionary<string, int> { { "a", 12 }, { "c", 3 } } }; // a changed, b removed, c added

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
        
        var propInfo = new CrdtPropertyInfo(
            "Map",
            "map",
            typeof(Dictionary<string, int>),
            true,
            true,
            obj => ((CounterMapTestModel)obj).Map,
            (obj, val) => ((CounterMapTestModel)obj).Map = (Dictionary<string, int>)val!,
            new CrdtCounterMapStrategyAttribute(),
            []);

        // Act & Assert
        strategy.GetStartKey(new CounterMapTestModel(), propInfo).ShouldBeNull();
        strategy.GetStartKey(new CounterMapTestModel { Map = { ["c"] = 1, ["a"] = 2, ["b"] = 3 } }, propInfo).ShouldBe("a");
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
        
        var stringMapProp = new CrdtPropertyInfo(
            "Map",
            "map",
            typeof(Dictionary<string, int>),
            true,
            true,
            obj => ((CounterMapTestModel)obj).Map,
            (obj, val) => ((CounterMapTestModel)obj).Map = (Dictionary<string, int>)val!,
            new CrdtCounterMapStrategyAttribute(),
            []);

        var intMapProp = new CrdtPropertyInfo(
            "Map",
            "map",
            typeof(Dictionary<int, int>),
            true,
            true,
            obj => ((CounterMapTestModelInt)obj).Map,
            (obj, val) => ((CounterMapTestModelInt)obj).Map = (Dictionary<int, int>)val!,
            new CrdtCounterMapStrategyAttribute(),
            []);

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
        
        var propInfo = new CrdtPropertyInfo(
            "Map",
            "map",
            typeof(Dictionary<string, int>),
            true,
            true,
            obj => ((CounterMapTestModel)obj).Map,
            (obj, val) => ((CounterMapTestModel)obj).Map = (Dictionary<string, int>)val!,
            new CrdtCounterMapStrategyAttribute(),
            []);

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

        var doc1 = (CounterMapTestModel)result.Partition1.Data;
        var doc2 = (CounterMapTestModel)result.Partition2.Data;

        doc1.Map.Keys.ShouldBe(["a", "b"], ignoreOrder: true);
        doc1.Map["a"].ShouldBe(10);
        doc1.Map["b"].ShouldBe(20);

        doc2.Map.Keys.ShouldBe(["c", "d"], ignoreOrder: true);
        doc2.Map["c"].ShouldBe(30);
        doc2.Map["d"].ShouldBe(40);

        ((CounterMapState)result.Partition1.Metadata.States["$.map"]).Keys.Keys.ShouldBe(["a", "b"], ignoreOrder: true);
        ((CounterMapState)result.Partition2.Metadata.States["$.map"]).Keys.Keys.ShouldBe(["c", "d"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_ShouldCombineDataAndMetadata()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();
        
        var propInfo = new CrdtPropertyInfo(
            "Map",
            "map",
            typeof(Dictionary<string, int>),
            true,
            true,
            obj => ((CounterMapTestModel)obj).Map,
            (obj, val) => ((CounterMapTestModel)obj).Map = (Dictionary<string, int>)val!,
            new CrdtCounterMapStrategyAttribute(),
            []);

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
        var mergedDoc = (CounterMapTestModel)merged.Data;
        mergedDoc.Map.Keys.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
        mergedDoc.Map["a"].ShouldBe(10);
        mergedDoc.Map["b"].ShouldBe(20);
        mergedDoc.Map["c"].ShouldBe(30);
        mergedDoc.Map["d"].ShouldBe(40);

        ((CounterMapState)merged.Metadata.States["$.map"]).Keys.Keys.ShouldBe(["a", "b", "c", "d"], ignoreOrder: true);
    }

    [Fact]
    public void Compact_ShouldNotModifyMetadata_AsStrategyDoesNotMaintainTombstones()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();

        var metadata = new CrdtMetadata();
        metadata.States["$.map"] = new CounterMapState(new Dictionary<object, PnCounterState>
        {
            { "a", new PnCounterState(10, 5) }
        });

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);

        var context = new CompactionContext(metadata, mockPolicy.Object, "Map", "$.map", new CounterMapTestModel());

        // Act
        strategy.Compact(context);

        // Assert
        var state = (CounterMapState)metadata.States["$.map"];
        state.Keys.ShouldContainKey("a");
        state.Keys["a"].P.ShouldBe(10);
        mockPolicy.Verify(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()), Times.Never);
    }
}