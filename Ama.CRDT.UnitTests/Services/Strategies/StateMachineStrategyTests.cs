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
using Xunit;

internal sealed class OrderStatusStateMachine : IStateMachine<string>
{
    public bool IsValidTransition(string from, string to)
    {
        if (from is null && to == "PENDING") return true; // Initial transition

        return (from, to) switch
        {
            ("PENDING", "PROCESSING") => true,
            ("PROCESSING", "SHIPPED") => true,
            _ => false
        };
    }
}

internal sealed class StateMachineTestModel
{
    [CrdtStateMachineStrategy(typeof(OrderStatusStateMachine))]
    public string Status { get; set; } = string.Empty;
}

public sealed class StateMachineStrategyTests : IDisposable
{
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly StateMachineStrategy strategyA;
    private readonly StateMachineStrategy strategyB;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    public StateMachineStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<StateMachineStrategyTestCrdtContext>()
            .AddSingleton<OrderStatusStateMachine>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        strategyA = scopeA.ServiceProvider.GetRequiredService<StateMachineStrategy>();
        strategyB = scopeB.ServiceProvider.GetRequiredService<StateMachineStrategy>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }
    
    private static CrdtPropertyInfo CreatePropertyInfo()
    {
        return new CrdtPropertyInfo(
            "Status", 
            "status", 
            typeof(string), 
            true, 
            true,
            obj => ((StateMachineTestModel)obj).Status,
            (obj, val) => ((StateMachineTestModel)obj).Status = (string)val!,
            new CrdtStateMachineStrategyAttribute(typeof(OrderStatusStateMachine)),
            Array.Empty<CrdtStrategyDecoratorAttribute>());
    }

    [Fact]
    public void GeneratePatch_WithValidTransition_ShouldCreateUpsertOperation()
    {
        // Arrange
        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = new CausalTimestamp(timestampProvider.Create(100), "A", 1) } };
        var property = CreatePropertyInfo();
        var changeTimestamp = timestampProvider.Create(200);
        var context = new GeneratePatchContext(
            operations, new List<DifferentiateObjectContext>(), "$.status", property, "PENDING", "PROCESSING", new StateMachineTestModel { Status = "PENDING" }, new StateMachineTestModel { Status = "PROCESSING" }, originalMeta, changeTimestamp, 0);

        // Act
        strategyA.GeneratePatch(context);

        // Assert
        operations.ShouldHaveSingleItem();
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.status");
        op.Value.ShouldBe("PROCESSING");
        op.Timestamp.ShouldBe(changeTimestamp);
    }

    [Fact]
    public void GeneratePatch_WithInvalidTransition_ShouldNotCreateOperation()
    {
        // Arrange
        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = new CausalTimestamp(timestampProvider.Create(100), "A", 1) } };
        var property = CreatePropertyInfo();
        var context = new GeneratePatchContext(
            operations, new List<DifferentiateObjectContext>(), "$.status", property, "PENDING", "SHIPPED", new StateMachineTestModel { Status = "PENDING" }, new StateMachineTestModel { Status = "SHIPPED" }, originalMeta, timestampProvider.Create(200), 0);

        // Act
        strategyA.GeneratePatch(context);

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void GenerateOperation_WithValidSetIntent_ShouldReturnUpsertOperation()
    {
        // Arrange
        var model = new StateMachineTestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new CausalTimestamp(timestampProvider.Create(100), "A", 1) } };
        var property = CreatePropertyInfo();
        var timestamp = timestampProvider.Create(200);
        var intent = new SetIntent("PROCESSING");
        var context = new GenerateOperationContext(model, metadata, "$.status", property, intent, timestamp, 0);

        // Act
        var operation = strategyA.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.status");
        operation.Value.ShouldBe("PROCESSING");
        operation.Timestamp.ShouldBe(timestamp);
        operation.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithInvalidStateTransition_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var model = new StateMachineTestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new CausalTimestamp(timestampProvider.Create(100), "A", 1) } };
        var property = CreatePropertyInfo();
        var timestamp = timestampProvider.Create(200);
        var intent = new SetIntent("SHIPPED"); // Invalid transition directly from PENDING
        var context = new GenerateOperationContext(model, metadata, "$.status", property, intent, timestamp, 0);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var model = new StateMachineTestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata();
        var property = CreatePropertyInfo();
        var timestamp = timestampProvider.Create(200);
        var intent = new IncrementIntent(1); // Not supported intent type
        var context = new GenerateOperationContext(model, metadata, "$.status", property, intent, timestamp, 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_WithValidTransitionAndNewerTimestamp_ShouldUpdateModel()
    {
        // Arrange
        var model = new StateMachineTestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new CausalTimestamp(timestampProvider.Create(100), "A", 1) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200), 0);
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);
        
        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].Timestamp.ShouldBe(timestampProvider.Create(200));
    }

    [Fact]
    public void ApplyOperation_WithInvalidTransition_ShouldNotUpdateModel()
    {
        // Arrange
        var model = new StateMachineTestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new CausalTimestamp(timestampProvider.Create(100), "A", 1) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(200), 0);
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PENDING");
        metadata.Lww["$.status"].Timestamp.ShouldBe(timestampProvider.Create(100));
    }
    
    [Fact]
    public void ApplyOperation_WithOlderTimestamp_ShouldNotUpdateModel()
    {
        // Arrange
        var model = new StateMachineTestModel { Status = "PROCESSING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new CausalTimestamp(timestampProvider.Create(300), "A", 1) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(200), 0);
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].Timestamp.ShouldBe(timestampProvider.Create(300));
    }

    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new StateMachineTestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200), 0);
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);
        model.Status.ShouldBe("PROCESSING");
        
        strategyA.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].Timestamp.ShouldBe(timestampProvider.Create(200));
    }

    [Fact]
    public void ApplyOperation_ConflictResolutionIsCommutative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(300), 0);

        var model1 = new StateMachineTestModel { Status = "PROCESSING" };
        var meta1 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op2));

        var model2 = new StateMachineTestModel { Status = "PROCESSING" };
        var meta2 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op1));

        model1.Status.ShouldBe("SHIPPED");
        meta1.Lww["$.status"].Timestamp.ShouldBe(timestampProvider.Create(300));
        
        model2.Status.ShouldBe("SHIPPED");
        meta2.Lww["$.status"].Timestamp.ShouldBe(timestampProvider.Create(300));
    }

    [Fact]
    public void Compact_ShouldNotModifyMetadata_AsStrategyDoesNotMaintainTombstones()
    {
        // Arrange
        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);
        var metadata = new CrdtMetadata();

        var context = new CompactionContext(metadata, mockPolicy.Object, "Status", "$.status", new StateMachineTestModel());

        // Act
        strategyA.Compact(context);

        // Assert
        mockPolicy.Verify(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()), Times.Never);
    }
}