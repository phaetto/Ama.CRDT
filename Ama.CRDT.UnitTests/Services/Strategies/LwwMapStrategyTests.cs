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
using System.Threading;
using Xunit;

public sealed class LwwMapStrategyTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<IElementComparerProvider> comparerProviderMock = new();
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtScopeFactory scopeFactory;

    public LwwMapStrategyTests()
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
    public void ApplyOperation_Commutativity_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<LwwMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 1 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 1 } });

        Thread.Sleep(5);
        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("b", 2), timestampProvider.Now());
        Thread.Sleep(5);
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Remove, new KeyValuePair<object, object?>("a", null), timestampProvider.Now());

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
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 2), timestampProvider.Now());
        
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
        doc.Metadata.LwwMaps["$.map"]["a"] = timestampProvider.Create(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        
        var newerTimestamp = timestampProvider.Create(DateTimeOffset.UtcNow.AddMilliseconds(50).ToUnixTimeMilliseconds());
        var olderTimestamp = timestampProvider.Create(DateTimeOffset.UtcNow.AddMilliseconds(-50).ToUnixTimeMilliseconds());

        var olderOp = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 0), olderTimestamp);
        var newerOp = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Upsert, new KeyValuePair<object, object?>("a", 2), newerTimestamp);
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, olderOp));
        doc.Data.Map["a"].ShouldBe(1); // older op ignored

        strategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, newerOp));
        
        // Assert
        doc.Data.Map["a"].ShouldBe(2); // newer op applied
    }

    private sealed class TestModel
    {
        [CrdtLwwMapStrategy]
        public Dictionary<string, int> Map { get; set; } = [];
    }
}