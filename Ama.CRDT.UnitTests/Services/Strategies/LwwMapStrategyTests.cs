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
using System.Threading;
using Xunit;

[CrdtAotType(typeof(LwwMapTestModel))]
[CrdtAotType(typeof(Dictionary<string, int>))]
internal partial class LwwMapTestCrdtAotContext : CrdtAotContext
{
}

internal sealed class LwwMapTestModel
{
    [CrdtLwwMapStrategy]
    public Dictionary<string, int> Map { get; set; } = [];
}

public sealed class LwwMapStrategyTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<IElementComparerProvider> comparerProviderMock = new();
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtScopeFactory scopeFactory;

    private readonly CrdtPropertyInfo mapProperty = new CrdtPropertyInfo(
        nameof(LwwMapTestModel.Map),
        "map",
        typeof(Dictionary<string, int>),
        true,
        true,
        obj => ((LwwMapTestModel)obj).Map,
        (obj, val) => ((LwwMapTestModel)obj).Map = (Dictionary<string, int>)val!,
        new CrdtLwwMapStrategyAttribute(),
        Array.Empty<CrdtStrategyDecoratorAttribute>()
    );

    public LwwMapStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
            .AddCrdtAotContext<LwwMapTestCrdtAotContext>()
            .AddSingleton(comparerProviderMock.Object)
            .AddCrdtTimestampProvider<EpochTimestampProvider>();

        serviceProvider = services.BuildServiceProvider();
        scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        
        using var scope = scopeFactory.CreateScope("test");
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        comparerProviderMock.Setup(p => p.GetComparer(It.IsAny<Type>())).Returns(EqualityComparer<object>.Default);
    }

    private CrdtDocument<LwwMapTestModel> CreateDocument(Dictionary<string, int> map)
    {
        var model = new LwwMapTestModel { Map = map };
        var metadata = metadataManager.Initialize(model);
        return new CrdtDocument<LwwMapTestModel>(model, metadata);
    }

    [Fact]
    public void ApplyOperation_Commutativity_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 1 } });

        Thread.Sleep(5);
        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 2), timestampProvider.Now(), 0);
        Thread.Sleep(5);
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Remove, new KeyValuePair<object, object?>("a", null), timestampProvider.Now(), 0);

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
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        Thread.Sleep(5);
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 2), timestampProvider.Now(), 0);
        
        var expectedMap = new Dictionary<string, int> { { "a", 2 } };

        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, op));

        // Assert
        doc.Data.Map.ShouldBe(expectedMap);
    }

    [Fact]
    public void ApplyOperation_LwwWins_ShouldApplyNewerOperation()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();

        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        doc.Metadata.LwwMaps["$.map"]["a"] = new CausalTimestamp(timestampProvider.Create(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), "A", 1);
        
        var newerTimestamp = timestampProvider.Create(DateTimeOffset.UtcNow.AddMilliseconds(50).ToUnixTimeMilliseconds());
        var olderTimestamp = timestampProvider.Create(DateTimeOffset.UtcNow.AddMilliseconds(-50).ToUnixTimeMilliseconds());

        var olderOp = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 0), olderTimestamp, 0);
        var newerOp = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 2), newerTimestamp, 0);
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, olderOp));
        doc.Data.Map["a"].ShouldBe(1); // older op ignored

        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, newerOp));
        
        // Assert
        doc.Data.Map["a"].ShouldBe(2); // newer op applied
    }

    [Fact]
    public void GenerateOperation_WithMapSetIntent_ShouldGenerateUpsertOperation()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int>());
        var intent = new MapSetIntent("myKey", 42);
        var timestamp = timestampProvider.Now();
        
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", mapProperty, intent, timestamp, 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.map");
        operation.ReplicaId.ShouldBe("A");
        operation.Timestamp.ShouldBe(timestamp);
        
        var payload = operation.Value.ShouldBeOfType<KeyValuePair<object, object?>>();
        payload.Key.ShouldBe("myKey");
        payload.Value.ShouldBe(42);
    }

    [Fact]
    public void GenerateOperation_WithMapRemoveIntent_ShouldGenerateRemoveOperation()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int> { { "myKey", 42 } });
        var intent = new MapRemoveIntent("myKey");
        var timestamp = timestampProvider.Now();
        
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", mapProperty, intent, timestamp, 0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Remove);
        operation.JsonPath.ShouldBe("$.map");
        operation.ReplicaId.ShouldBe("A");
        operation.Timestamp.ShouldBe(timestamp);
        
        var payload = operation.Value.ShouldBeOfType<KeyValuePair<object, object?>>();
        payload.Key.ShouldBe("myKey");
        payload.Value.ShouldBeNull();
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int>());
        var intent = new AddIntent(42);
        var timestamp = timestampProvider.Now();
        
        var context = new GenerateOperationContext(doc.Data, doc.Metadata, "$.map", mapProperty, intent, timestamp, 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }

    [Fact]
    public void GetStartKey_ShouldReturnSmallestKey()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var doc = new LwwMapTestModel { Map = new Dictionary<string, int> { { "c", 3 }, { "a", 1 }, { "b", 2 } } };

        // Act
        var startKey = strategy.GetStartKey(doc, mapProperty);

        // Assert
        startKey.ShouldBe("a");
    }

    [Fact]
    public void GetStartKey_EmptyDictionary_ShouldReturnNull()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var doc = new LwwMapTestModel { Map = new Dictionary<string, int>() };

        // Act
        var startKey = strategy.GetStartKey(doc, mapProperty);

        // Assert
        startKey.ShouldBeNull();
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractKey()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("myKey", 42), timestampProvider.Now(), 0);

        // Act
        var key = strategy.GetKeyFromOperation(op, "$.map");

        // Assert
        key.ShouldBe("myKey");
    }

    [Fact]
    public void GetKeyFromOperation_MismatchedPath_ShouldReturnNull()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.otherMap", OperationType.Upsert, new KeyValuePair<object, object?>("myKey", 42), timestampProvider.Now(), 0);

        // Act
        var key = strategy.GetKeyFromOperation(op, "$.map");

        // Assert
        key.ShouldBeNull();
    }

    [Fact]
    public void GetMinimumKey_ShouldReturnEmptyStringForStringKeys()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();

        // Act
        var minKey = strategy.GetMinimumKey(mapProperty);

        // Assert
        minKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void Split_ShouldDivideDataAndMetadata()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 }, { "b", 2 }, { "c", 3 }, { "d", 4 } });
        
        doc.Metadata.LwwMaps["$.map"] = new Dictionary<object, CausalTimestamp>
        {
            { "a", new CausalTimestamp(timestampProvider.Create(1), "A", 1) },
            { "b", new CausalTimestamp(timestampProvider.Create(2), "A", 2) },
            { "c", new CausalTimestamp(timestampProvider.Create(3), "A", 3) },
            { "d", new CausalTimestamp(timestampProvider.Create(4), "A", 4) }
        };

        // Act
        var result = strategy.Split(doc.Data, doc.Metadata, mapProperty);

        // Assert
        result.SplitKey.ShouldBe("c");
        
        var doc1 = (LwwMapTestModel)result.Partition1.Data;
        var meta1 = result.Partition1.Metadata;
        doc1.Map.Count.ShouldBe(2);
        doc1.Map.ShouldContainKey("a");
        doc1.Map.ShouldContainKey("b");

        meta1.LwwMaps["$.map"].Count.ShouldBe(2);
        meta1.LwwMaps["$.map"].ShouldContainKey("a");
        meta1.LwwMaps["$.map"].ShouldContainKey("b");

        var doc2 = (LwwMapTestModel)result.Partition2.Data;
        var meta2 = result.Partition2.Metadata;
        doc2.Map.Count.ShouldBe(2);
        doc2.Map.ShouldContainKey("c");
        doc2.Map.ShouldContainKey("d");

        meta2.LwwMaps["$.map"].Count.ShouldBe(2);
        meta2.LwwMaps["$.map"].ShouldContainKey("c");
        meta2.LwwMaps["$.map"].ShouldContainKey("d");
    }

    [Fact]
    public void Split_WithLessThanTwoItems_ShouldThrow()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        var doc = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        
        doc.Metadata.LwwMaps["$.map"] = new Dictionary<object, CausalTimestamp>
        {
            { "a", new CausalTimestamp(timestampProvider.Create(1), "A", 1) }
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.Split(doc.Data, doc.Metadata, mapProperty));
    }

    [Fact]
    public void Merge_ShouldCombineDataAndMetadata()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 1 }, { "overlap", 100 } });
        doc1.Metadata.LwwMaps["$.map"] = new Dictionary<object, CausalTimestamp>
        {
            { "a", new CausalTimestamp(timestampProvider.Create(1), "A", 1) },
            { "overlap", new CausalTimestamp(timestampProvider.Create(100), "A", 100) }
        };

        var doc2 = CreateDocument(new Dictionary<string, int> { { "c", 3 }, { "overlap", 200 } });
        doc2.Metadata.LwwMaps["$.map"] = new Dictionary<object, CausalTimestamp>
        {
            { "c", new CausalTimestamp(timestampProvider.Create(3), "B", 3) },
            { "overlap", new CausalTimestamp(timestampProvider.Create(200), "B", 200) } // Higher timestamp, should win
        };

        // Act
        var result = strategy.Merge(doc1.Data, doc1.Metadata, doc2.Data, doc2.Metadata, mapProperty);

        // Assert
        var mergedDoc = (LwwMapTestModel)result.Data;
        var mergedMeta = result.Metadata;

        mergedDoc.Map.Count.ShouldBe(3);
        mergedDoc.Map["a"].ShouldBe(1);
        mergedDoc.Map["c"].ShouldBe(3);
        mergedDoc.Map["overlap"].ShouldBe(200);

        mergedMeta.LwwMaps["$.map"].Count.ShouldBe(3);
        mergedMeta.LwwMaps["$.map"]["overlap"].ShouldBe(new CausalTimestamp(timestampProvider.Create(200), "B", 200));
    }

    [Fact]
    public void Compact_ShouldRemoveTombstones_WhenPolicyAllows()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();
        
        var doc = new LwwMapTestModel { Map = new Dictionary<string, int> { { "alive", 1 } } };
        var meta = new CrdtMetadata();

        var tsAlive = new CausalTimestamp(timestampProvider.Create(1), "replica-1", 1);
        var tsDeadSafe = new CausalTimestamp(timestampProvider.Create(2), "replica-1", 2);
        var tsDeadUnsafe = new CausalTimestamp(timestampProvider.Create(3), "replica-2", 3);

        meta.LwwMaps["$.map"] = new Dictionary<object, CausalTimestamp>(EqualityComparer<object>.Default)
        {
            { "alive", tsAlive },
            { "dead_safe", tsDeadSafe },
            { "dead_unsafe", tsDeadUnsafe }
        };

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "replica-1" && c.Version <= 2))).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "replica-2" && c.Version == 3))).Returns(false);
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "replica-1" && c.Version == 1))).Returns(true); // Shouldn't be checked anyway

        var context = new CompactionContext(meta, mockPolicy.Object, "Map", "$.map", doc);

        // Act
        strategy.Compact(context);

        // Assert
        meta.LwwMaps["$.map"].ShouldContainKey("alive");
        meta.LwwMaps["$.map"].ShouldContainKey("dead_unsafe");
        meta.LwwMaps["$.map"].ShouldNotContainKey("dead_safe");
        
        // Verify alive was not checked
        mockPolicy.Verify(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "replica-1" && c.Version == 1)), Times.Never);
    }
}