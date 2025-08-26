namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    
    private readonly StateMachineStrategy strategy;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    public StateMachineStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<OrderStatusStateMachine>()
            .BuildServiceProvider();
            
        var options = Options.Create(new CrdtOptions { ReplicaId = "replica-A" });

        strategy = new StateMachineStrategy(options, serviceProvider);
    }
    
    [Fact]
    public void GeneratePatch_WithValidTransitionAndNewerTimestamp_ShouldCreateUpsertOperation()
    {
        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = new EpochTimestamp(100) } };
        var modifiedMeta = new CrdtMetadata { Lww = { ["$.status"] = new EpochTimestamp(200) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status));

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.status", property, "PENDING", "PROCESSING", new TestModel { Status = "PENDING" }, new TestModel { Status = "PROCESSING" }, originalMeta, modifiedMeta);

        operations.ShouldHaveSingleItem();
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.status");
        op.Value.ShouldBe("PROCESSING");
        op.Timestamp.ShouldBe(new EpochTimestamp(200));
    }

    [Fact]
    public void GeneratePatch_WithInvalidTransition_ShouldNotCreateOperation()
    {
        var originalMeta = new CrdtMetadata { Lww = { ["$.status"] = new EpochTimestamp(100) } };
        var modifiedMeta = new CrdtMetadata { Lww = { ["$.status"] = new EpochTimestamp(200) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Status));

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.status", property, "PENDING", "SHIPPED", new TestModel { Status = "PENDING" }, new TestModel { Status = "SHIPPED" }, originalMeta, modifiedMeta);

        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void ApplyOperation_WithValidTransitionAndNewerTimestamp_ShouldUpdateModel()
    {
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new EpochTimestamp(100) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", new EpochTimestamp(200));

        strategy.ApplyOperation(model, metadata, operation);
        
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(new EpochTimestamp(200));
    }

    [Fact]
    public void ApplyOperation_WithInvalidTransition_ShouldNotUpdateModel()
    {
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new EpochTimestamp(100) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", new EpochTimestamp(200));

        strategy.ApplyOperation(model, metadata, operation);

        model.Status.ShouldBe("PENDING");
        metadata.Lww["$.status"].ShouldBe(new EpochTimestamp(100));
    }
    
    [Fact]
    public void ApplyOperation_WithOlderTimestamp_ShouldNotUpdateModel()
    {
        var model = new TestModel { Status = "PROCESSING" };
        var metadata = new CrdtMetadata { Lww = { ["$.status"] = new EpochTimestamp(300) } };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "SHIPPED", new EpochTimestamp(200));

        strategy.ApplyOperation(model, metadata, operation);

        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(new EpochTimestamp(300));
    }

    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        var model = new TestModel { Status = "PENDING" };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.status", OperationType.Upsert, "PROCESSING", new EpochTimestamp(200));

        strategy.ApplyOperation(model, metadata, operation);
        model.Status.ShouldBe("PROCESSING");
        
        strategy.ApplyOperation(model, metadata, operation);
        model.Status.ShouldBe("PROCESSING");
        metadata.Lww["$.status"].ShouldBe(new EpochTimestamp(200));
    }

    [Fact]
    public void ApplyOperation_ConflictResolutionIsCommutative()
    {
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.status", OperationType.Upsert, "PROCESSING", new EpochTimestamp(200));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.status", OperationType.Upsert, "SHIPPED", new EpochTimestamp(300));

        var model1 = new TestModel { Status = "PROCESSING" };
        var meta1 = new CrdtMetadata();
        strategy.ApplyOperation(model1, meta1, op1);
        strategy.ApplyOperation(model1, meta1, op2);

        var model2 = new TestModel { Status = "PROCESSING" };
        var meta2 = new CrdtMetadata();
        strategy.ApplyOperation(model2, meta2, op2);
        strategy.ApplyOperation(model2, meta2, op1);

        model1.Status.ShouldBe("SHIPPED");
        meta1.Lww["$.status"].ShouldBe(new EpochTimestamp(300));
        
        model2.Status.ShouldBe("SHIPPED");
        meta2.Lww["$.status"].ShouldBe(new EpochTimestamp(300));
    }
}