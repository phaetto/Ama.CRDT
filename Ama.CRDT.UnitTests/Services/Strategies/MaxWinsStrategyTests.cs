namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class MaxWinsStrategyTests : IDisposable
{
    private sealed class TestModel { public int HighScore { get; set; } }
    
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly MaxWinsStrategy strategyA;
    private readonly MaxWinsStrategy strategyB;
    private readonly ICrdtTimestampProvider timestampProvider;

    public MaxWinsStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        strategyA = scopeA.ServiceProvider.GetRequiredService<MaxWinsStrategy>();
        strategyB = scopeB.ServiceProvider.GetRequiredService<MaxWinsStrategy>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsert_WhenValueChanges()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.HighScore))!;
        var context = new GeneratePatchContext(
            operations,
            new List<DifferentiateObjectContext>(),
            "$.highScore",
            property,
            100,
            200,
            new TestModel { HighScore = 100 },
            new TestModel { HighScore = 200 },
            new CrdtMetadata(),
            timestampProvider.Now(),
            0
        );
        
        // Act
        strategyA.GeneratePatch(context);

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(200);
    }

    [Fact]
    public void GenerateOperation_ShouldCreateUpsert_WhenIntentIsSetIntent()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.HighScore))!;
        var intent = new SetIntent(300);
        var timestamp = timestampProvider.Now();
        var context = new GenerateOperationContext(
            new TestModel(),
            new CrdtMetadata(),
            "$.highScore",
            property,
            intent,
            timestamp,
            0
        );

        // Act
        var operation = strategyA.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.Value.ShouldBe(300);
        operation.JsonPath.ShouldBe("$.highScore");
        operation.Timestamp.ShouldBe(timestamp);
    }

    [Fact]
    public void GenerateOperation_ShouldThrowNotSupportedException_WhenIntentIsInvalid()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.HighScore))!;
        var intent = new RemoveIntent(0); // Invalid intent for MaxWinsStrategy
        var context = new GenerateOperationContext(
            new TestModel(),
            new CrdtMetadata(),
            "$.highScore",
            property,
            intent,
            timestampProvider.Now(),
            0
        );

        // Act
        var exception = Record.Exception(() => strategyA.GenerateOperation(context));
        
        // Assert
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<NotSupportedException>();
        exception.Message.ShouldContain("RemoveIntent");
        exception.Message.ShouldContain("MaxWinsStrategy");
    }
    
    [Fact]
    public void ApplyOperation_ShouldUpdate_WhenIncomingIsHigher()
    {
        // Arrange
        var model = new TestModel { HighScore = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L), 0);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
        
        // Act
        strategyA.ApplyOperation(context);
        
        // Assert
        model.HighScore.ShouldBe(200);
    }
    
    [Fact]
    public void ApplyOperation_ShouldNotUpdate_WhenIncomingIsLower()
    {
        // Arrange
        var model = new TestModel { HighScore = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.highScore", OperationType.Upsert, 100, timestampProvider.Create(2L), 0);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
        
        // Act
        strategyA.ApplyOperation(context);
        
        // Assert
        model.HighScore.ShouldBe(150);
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { HighScore = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L), 0);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
    
        // Act
        strategyA.ApplyOperation(context);
        var scoreAfterFirst = model.HighScore;
        strategyA.ApplyOperation(context);
    
        // Assert
        model.HighScore.ShouldBe(scoreAfterFirst);
        model.HighScore.ShouldBe(200);
    }

    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var model1 = new TestModel { HighScore = 100 };
        var model2 = new TestModel { HighScore = 100 };
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.highScore", OperationType.Upsert, 150, timestampProvider.Create(3L), 0);

        // Act
        // op1 then op2
        strategyA.ApplyOperation(new ApplyOperationContext(model1, new CrdtMetadata(), op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, new CrdtMetadata(), op2));

        // op2 then op1
        strategyA.ApplyOperation(new ApplyOperationContext(model2, new CrdtMetadata(), op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, new CrdtMetadata(), op1));
    
        // Assert
        model1.HighScore.ShouldBe(200); // max(100, 200, 150)
        model2.HighScore.ShouldBe(200);
        model1.HighScore.ShouldBe(model2.HighScore);
    }
    
    [Fact]
    public void ApplyOperation_IsAssociative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.highScore", OperationType.Upsert, 200, timestampProvider.Create(2L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.highScore", OperationType.Upsert, 150, timestampProvider.Create(3L), 0);
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.highScore", OperationType.Upsert, 250, timestampProvider.Create(4L), 0);

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
                strategyA.ApplyOperation(new ApplyOperationContext(model, meta, op));
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