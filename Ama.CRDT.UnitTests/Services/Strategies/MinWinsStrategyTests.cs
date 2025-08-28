namespace Ama.CRDT.UnitTests.Services.Strategies;

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

public sealed class MinWinsStrategyTests
{
    private sealed class TestModel { public int BestTime { get; set; } }
    
    private readonly IServiceProvider serviceProvider;
    private readonly Mock<ICrdtTimestampProvider> mockTimestampProvider = new();
    private readonly string replicaId = Guid.NewGuid().ToString();

    public MinWinsStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddSingleton(mockTimestampProvider.Object);
        
        serviceProvider = services.BuildServiceProvider();
        mockTimestampProvider.Setup(p => p.Now()).Returns(new EpochTimestamp(1L));
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsert_WhenNewValueIsLower()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsStrategy>();

        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        
        // Act
        strategy.GeneratePatch(new Mock<ICrdtPatcher>().Object, operations, "$.bestTime", property, 200, 100, new TestModel { BestTime = 200 }, new TestModel { BestTime = 100 }, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(100);
    }
    
    [Fact]
    public void GeneratePatch_ShouldDoNothing_WhenNewValueIsHigher()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsStrategy>();

        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        
        // Act
        strategy.GeneratePatch(new Mock<ICrdtPatcher>().Object, operations, "$.bestTime", property, 100, 200, new TestModel { BestTime = 100 }, new TestModel { BestTime = 200 }, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void ApplyOperation_ShouldUpdate_WhenIncomingIsLower()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsStrategy>();

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
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsStrategy>();

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
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsStrategy>();

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
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsStrategy>();

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
    
    [Fact]
    public void ApplyOperation_IsAssociative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MinWinsStrategy>();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.bestTime", OperationType.Upsert, 200, new EpochTimestamp(2L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.bestTime", OperationType.Upsert, 250, new EpochTimestamp(3L));
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.bestTime", OperationType.Upsert, 150, new EpochTimestamp(4L));

        var ops = new[] { op1, op2, op3 };
        var permutations = GetPermutations(ops, ops.Length);
        var finalTimes = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { BestTime = 300 };
            var meta = new CrdtMetadata();
            foreach (var op in p)
            {
                strategy.ApplyOperation(model, meta, op);
            }
            finalTimes.Add(model.BestTime);
        }

        // Assert
        // The lowest value wins (op3 with value 150)
        finalTimes.ShouldAllBe(t => t == 150);
    }
    
    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
}