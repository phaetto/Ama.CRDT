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
            .AddSingleton(comparerProviderMock.Object)
            .AddCrdtTimestampProvider<SequentialTimestampProvider>();

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
    public void ApplyOperation_Commutative_ShouldConverge()
    {
        // Arrange
        using var scope = scopeFactory.CreateScope("A");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterMapStrategy>();

        var doc1 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });
        var doc2 = CreateDocument(new Dictionary<string, int> { { "a", 10 } });

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now());
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("b", 2), timestampProvider.Now());
        var op3 = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", -3), timestampProvider.Now());
        
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

        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now());
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("b", 2), timestampProvider.Now());
        var op3 = new CrdtOperation(Guid.NewGuid(), "C", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", -3), timestampProvider.Now());

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
        var op = new CrdtOperation(Guid.NewGuid(), "A", "$.map", OperationType.Increment, new KeyValuePair<object, object?>("a", 5), timestampProvider.Now());

        // Act
        // Simulate the CrdtApplicator's role of filtering seen operations.
        // First time, the operation is new.
        applicator.ApplyPatch(doc, new CrdtPatch { Operations = [ op ] });

        var stateAfterFirstApply = new Dictionary<string, int>(doc.Data.Map);
        var metadataCountersAfterFirstApply = doc.Metadata.CounterMaps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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

    private sealed class TestModel
    {
        [CrdtCounterMapStrategy]
        public Dictionary<string, int> Map { get; set; } = [];
    }
}