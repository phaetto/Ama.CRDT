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

public sealed class MaxWinsStrategyTests
{
    private sealed class TestModel { public int HighScore { get; set; } }
    
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly string replicaId = Guid.NewGuid().ToString();

    public MaxWinsStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, SequentialTimestampProvider>();

        serviceProvider = services.BuildServiceProvider();
        timestampProvider = serviceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsert_WhenValueChanges()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsStrategy>();

        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.HighScore))!;
        var context = new GeneratePatchContext(
            new Mock<ICrdtPatcher>().Object,
            operations,
            "$.highScore",
            property,
            100,
            200,
            new TestModel { HighScore = 100 },
            new TestModel { HighScore = 200 },
            new CrdtMetadata(),
            timestampProvider.Now()
        );
        
        // Act
        strategy.GeneratePatch(context);

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(200);
    }
    
    [Fact]
    public void ApplyOperation_ShouldUpdate_WhenIncomingIsHigher()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsStrategy>();

        var model = new TestModel { HighScore = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
        
        // Act
        strategy.ApplyOperation(context);
        
        // Assert
        model.HighScore.ShouldBe(200);
    }
    
    [Fact]
    public void ApplyOperation_ShouldNotUpdate_WhenIncomingIsLower()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsStrategy>();

        var model = new TestModel { HighScore = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.highScore", OperationType.Upsert, 100, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
        
        // Act
        strategy.ApplyOperation(context);
        
        // Assert
        model.HighScore.ShouldBe(150);
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsStrategy>();

        var model = new TestModel { HighScore = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
    
        // Act
        strategy.ApplyOperation(context);
        var scoreAfterFirst = model.HighScore;
        strategy.ApplyOperation(context);
    
        // Assert
        model.HighScore.ShouldBe(scoreAfterFirst);
        model.HighScore.ShouldBe(200);
    }

    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsStrategy>();

        var model1 = new TestModel { HighScore = 100 };
        var model2 = new TestModel { HighScore = 100 };
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.highScore", OperationType.Upsert, 150, timestampProvider.Create(3L));

        // Act
        // op1 then op2
        strategy.ApplyOperation(new ApplyOperationContext(model1, new CrdtMetadata(), op1));
        strategy.ApplyOperation(new ApplyOperationContext(model1, new CrdtMetadata(), op2));

        // op2 then op1
        strategy.ApplyOperation(new ApplyOperationContext(model2, new CrdtMetadata(), op2));
        strategy.ApplyOperation(new ApplyOperationContext(model2, new CrdtMetadata(), op1));
    
        // Assert
        model1.HighScore.ShouldBe(200); // max(100, 200, 150)
        model2.HighScore.ShouldBe(200);
        model1.HighScore.ShouldBe(model2.HighScore);
    }
    
    [Fact]
    public void ApplyOperation_IsAssociative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<MaxWinsStrategy>();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.highScore", OperationType.Upsert, 150, timestampProvider.Create(3L));
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.highScore", OperationType.Upsert, 250, timestampProvider.Create(4L));

        var ops = new[] { op1, op2, op3 };
        var permutations = GetPermutations(ops, ops.Length);
        var finalScores = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { HighScore = 100 };
            var meta = new CrdtMetadata();
            foreach (var op in p)
            {
                strategy.ApplyOperation(new ApplyOperationContext(model, meta, op));
            }
            finalScores.Add(model.HighScore);
        }

        // Assert
        // The highest value wins (op3 with value 250)
        finalScores.ShouldAllBe(s => s == 250);
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