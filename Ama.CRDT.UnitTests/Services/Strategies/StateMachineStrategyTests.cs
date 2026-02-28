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
using Xunit;

public sealed class StateMachineStrategyTests : IDisposable
{
    // Test validator for order status transitions
    private sealed class OrderStatusStateMachine : IStateMachine<string>
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

    private sealed class TestModel
    {
        [CrdtStateMachineStrategy(typeof(OrderStatusStateMachine))]
        public string Status { get; set; }
    }
    
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
    
    [Fact]
    public void GeneratePatch_WithValidTransition_ShouldCreateUpsertOperation()
    {
        // Arrange
        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status));
        var changeTimestamp = timestampProvider.Create(200);
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.status", property, "PENDING", "PROCESSING", new TestModel { Status = "PENDING" }, new TestModel { Status = "PROCESSING" }, originalMeta, changeTimestamp);

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
        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status));
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.status", property, "PENDING", "SHIPPED", new TestModel { Status = "PENDING" }, new TestModel { Status = "SHIPPED" }, originalMeta, timestampProvider.Create(200));

        // Act
        strategyA.GeneratePatch(context);

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void GenerateOperation_WithValidSetIntent_ShouldReturnUpsertOperation()
    {
        // Arrange
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status))!;
        var timestamp = timestampProvider.Create(200);
        var intent = new SetIntent("PROCESSING");
        var context = new GenerateOperationContext(model, metadata, "$.status", property, intent, timestamp, "r1");

        // Act
        var operation = strategyA.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.status");
        operation.Value.ShouldBe("PROCESSING");
        operation.Timestamp.ShouldBe(timestamp);
        operation.ReplicaId.ShouldBe("r1");
    }

    [Fact]
    public void GenerateOperation_WithInvalidStateTransition_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status))!;
        var timestamp = timestampProvider.Create(200);
        var intent = new SetIntent("SHIPPED"); // Invalid transition directly from PENDING
        var context = new GenerateOperationContext(model, metadata, "$.status", property, intent, timestamp, "r1");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status))!;
        var timestamp = timestampProvider.Create(200);
        var intent = new IncrementIntent(1); // Not supported intent type
        var context = new GenerateOperationContext(model, metadata, "$.status", property, intent, timestamp, "r1");

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_WithValidTransitionAndNewerTimestamp_ShouldUpdateModel()
    {
        // Arrange
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);
        
        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(200));
    }

    [Fact]
    public void ApplyOperation_WithInvalidTransition_ShouldNotUpdateModel()
    {
        // Arrange
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PENDING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(100));
    }
    
    [Fact]
    public void ApplyOperation_WithOlderTimestamp_ShouldNotUpdateModel()
    {
        // Arrange
        var model = new TestModel { Status = "PROCESSING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(300) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(300));
    }

    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);
        model.Status.ShouldBe("PROCESSING");
        
        strategyA.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(200));
    }

    [Fact]
    public void ApplyOperation_ConflictResolutionIsCommutative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(300));

        var model1 = new TestModel { Status = "PROCESSING" };
        var meta1 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op2));

        var model2 = new TestModel { Status = "PROCESSING" };
        var meta2 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op1));

        model1.Status.ShouldBe("SHIPPED");
        meta1.Lww["$.status"].ShouldBe(timestampProvider.Create(300));
        
        model2.Status.ShouldBe("SHIPPED");
        meta2.Lww["$.status"].ShouldBe(timestampProvider.Create(300));
    }
}