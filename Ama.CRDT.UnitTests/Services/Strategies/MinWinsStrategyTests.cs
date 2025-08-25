namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System.Collections.Generic;
using Xunit;

public sealed class MinWinsStrategyTests
{
    private sealed class TestModel { public int BestTime { get; set; } }
    
    private readonly MinWinsStrategy strategy;
    private readonly Mock<ICrdtTimestampProvider> mockTimestampProvider = new();

    public MinWinsStrategyTests()
    {
        strategy = new MinWinsStrategy(Options.Create(new CrdtOptions { ReplicaId = Guid.NewGuid().ToString() }), mockTimestampProvider.Object);
        mockTimestampProvider.Setup(p => p.Now()).Returns(new EpochTimestamp(1L));
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsert_WhenNewValueIsLower()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        
        // Act
        strategy.GeneratePatch(new Mock<ICrdtPatcher>().Object, operations, "$.bestTime", property, 200, 100, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(100);
    }
    
    [Fact]
    public void GeneratePatch_ShouldDoNothing_WhenNewValueIsHigher()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        
        // Act
        strategy.GeneratePatch(new Mock<ICrdtPatcher>().Object, operations, "$.bestTime", property, 100, 200, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void ApplyOperation_ShouldUpdate_WhenIncomingIsLower()
    {
        // Arrange
        var model = new TestModel { BestTime = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.bestTime", OperationType.Upsert, 100, new EpochTimestamp(2L));
        
        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);
        
        // Assert
        model.BestTime.ShouldBe(100);
    }
    
    [Fact]
    public void ApplyOperation_ShouldNotUpdate_WhenIncomingIsHigher()
    {
        // Arrange
        var model = new TestModel { BestTime = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.bestTime", OperationType.Upsert, 200, new EpochTimestamp(2L));
        
        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);
        
        // Assert
        model.BestTime.ShouldBe(150);
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { BestTime = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.bestTime", OperationType.Upsert, 100, new EpochTimestamp(2L));
    
        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);
        var scoreAfterFirst = model.BestTime;
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);
    
        // Assert
        model.BestTime.ShouldBe(scoreAfterFirst);
        model.BestTime.ShouldBe(100);
    }

    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var model1 = new TestModel { BestTime = 300 };
        var model2 = new TestModel { BestTime = 300 };
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.bestTime", OperationType.Upsert, 200, new EpochTimestamp(2L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.bestTime", OperationType.Upsert, 250, new EpochTimestamp(3L));

        // Act
        // op1 then op2
        strategy.ApplyOperation(model1, new CrdtMetadata(), op1);
        strategy.ApplyOperation(model1, new CrdtMetadata(), op2);

        // op2 then op1
        strategy.ApplyOperation(model2, new CrdtMetadata(), op2);
        strategy.ApplyOperation(model2, new CrdtMetadata(), op1);
    
        // Assert
        model1.BestTime.ShouldBe(200); // min(300, 200, 250)
        model2.BestTime.ShouldBe(200);
        model1.BestTime.ShouldBe(model2.BestTime);
    }
}