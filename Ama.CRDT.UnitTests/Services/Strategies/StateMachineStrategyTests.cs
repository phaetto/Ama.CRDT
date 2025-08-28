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
using Xunit;

public sealed class StateMachineStrategyTests
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
    
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    public StateMachineStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, SequentialTimestampProvider>()
            .AddSingleton<OrderStatusStateMachine>();

        serviceProvider = services.BuildServiceProvider();
        timestampProvider = serviceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }
    
    [Fact]
    public void GeneratePatch_WithValidTransition_ShouldCreateUpsertOperation()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("replica-A");
        var strategy = scope.ServiceProvider.GetRequiredService<StateMachineStrategy>();

        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status));
        var changeTimestamp = timestampProvider.Create(200);
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.status", property, "PENDING", "PROCESSING", new TestModel { Status = "PENDING" }, new TestModel { Status = "PROCESSING" }, originalMeta, changeTimestamp);

        // Act
        strategy.GeneratePatch(context);

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
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("replica-A");
        var strategy = scope.ServiceProvider.GetRequiredService<StateMachineStrategy>();

        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status));
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.status", property, "PENDING", "SHIPPED", new TestModel { Status = "PENDING" }, new TestModel { Status = "SHIPPED" }, originalMeta, timestampProvider.Create(200));

        // Act
        strategy.GeneratePatch(context);

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void ApplyOperation_WithValidTransitionAndNewerTimestamp_ShouldUpdateModel()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("replica-A");
        var strategy = scope.ServiceProvider.GetRequiredService<StateMachineStrategy>();

        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategy.ApplyOperation(context);
        
        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(200));
    }

    [Fact]
    public void ApplyOperation_WithInvalidTransition_ShouldNotUpdateModel()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("replica-A");
        var strategy = scope.ServiceProvider.GetRequiredService<StateMachineStrategy>();

        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(100) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategy.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PENDING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(100));
    }
    
    [Fact]
    public void ApplyOperation_WithOlderTimestamp_ShouldNotUpdateModel()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("replica-A");
        var strategy = scope.ServiceProvider.GetRequiredService<StateMachineStrategy>();

        var model = new TestModel { Status = "PROCESSING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = timestampProvider.Create(300) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategy.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(300));
    }

    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("replica-A");
        var strategy = scope.ServiceProvider.GetRequiredService<StateMachineStrategy>();

        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategy.ApplyOperation(context);
        model.Status.ShouldBe("PROCESSING");
        
        strategy.ApplyOperation(context);

        // Assert
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(timestampProvider.Create(200));
    }

    [Fact]
    public void ApplyOperation_ConflictResolutionIsCommutative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("replica-A");
        var strategy = scope.ServiceProvider.GetRequiredService<StateMachineStrategy>();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.status", OperationType.Upsert, "PROCESSING", timestampProvider.Create(200));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.status", OperationType.Upsert, "SHIPPED", timestampProvider.Create(300));

        var model1 = new TestModel { Status = "PROCESSING" };
        var meta1 = new CrdtMetadata();
        strategy.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategy.ApplyOperation(new ApplyOperationContext(model1, meta1, op2));

        var model2 = new TestModel { Status = "PROCESSING" };
        var meta2 = new CrdtMetadata();
        strategy.ApplyOperation(new ApplyOperationContext(model2, meta2, op2));
        strategy.ApplyOperation(new ApplyOperationContext(model2, meta2, op1));

        model1.Status.ShouldBe("SHIPPED");
        meta1.Lww["$.status"].ShouldBe(timestampProvider.Create(300));
        
        model2.Status.ShouldBe("SHIPPED");
        meta2.Lww["$.status"].ShouldBe(timestampProvider.Create(300));
    }
}