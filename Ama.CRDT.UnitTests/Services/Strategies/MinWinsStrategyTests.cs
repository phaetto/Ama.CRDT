namespace Ama.CRDT.UnitTests.Services.Strategies;

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
using System.Linq;
using Xunit;

public sealed class MinWinsStrategyTests : IDisposable
{
    private sealed class TestModel { public int BestTime { get; set; } }
    
    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly MinWinsStrategy strategyA;
    private readonly MinWinsStrategy strategyB;
    private readonly ICrdtTimestampProvider timestampProvider;

    public MinWinsStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        strategyA = scopeA.ServiceProvider.GetRequiredService<MinWinsStrategy>();
        strategyB = scopeB.ServiceProvider.GetRequiredService<MinWinsStrategy>();
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
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        var context = new GeneratePatchContext(
            operations,
            new List<DifferentiateObjectContext>(),
            "$.bestTime",
            property,
            200,
            100,
            new TestModel { BestTime = 200 },
            new TestModel { BestTime = 100 },
            new CrdtMetadata(),
            timestampProvider.Now()
        );
        
        // Act
        strategyA.GeneratePatch(context);

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(100);
    }
    
    [Fact]
    public void GenerateOperation_ShouldCreateUpsert_WhenIntentIsSetIntent()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        var context = new GenerateOperationContext(
            new TestModel(),
            new CrdtMetadata(),
            "$.bestTime",
            property,
            new SetIntent(100),
            timestampProvider.Now(),
            "r1"
        );

        // Act
        var result = strategyA.GenerateOperation(context);

        // Assert
        result.Type.ShouldBe(OperationType.Upsert);
        result.Value.ShouldBe(100);
        result.JsonPath.ShouldBe("$.bestTime");
        result.ReplicaId.ShouldBe("r1");
    }

    [Fact]
    public void GenerateOperation_ShouldThrowArgumentException_WhenValueIsNotComparable()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        var context = new GenerateOperationContext(
            new TestModel(),
            new CrdtMetadata(),
            "$.bestTime",
            property,
            new SetIntent(new object()), // object is not IComparable
            timestampProvider.Now(),
            "r1"
        );

        // Act & Assert
        Should.Throw<ArgumentException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void GenerateOperation_ShouldThrowNotSupportedException_WhenIntentIsInvalid()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.BestTime))!;
        var context = new GenerateOperationContext(
            new TestModel(),
            new CrdtMetadata(),
            "$.bestTime",
            property,
            new RemoveIntent(0),
            timestampProvider.Now(),
            "r1"
        );

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_ShouldUpdate_WhenIncomingIsLower()
    {
        // Arrange
        var model = new TestModel { BestTime = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.bestTime", OperationType.Upsert, 100, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
        
        // Act
        strategyA.ApplyOperation(context);
        
        // Assert
        model.BestTime.ShouldBe(100);
    }
    
    [Fact]
    public void ApplyOperation_ShouldNotUpdate_WhenIncomingIsHigher()
    {
        // Arrange
        var model = new TestModel { BestTime = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.bestTime", OperationType.Upsert, 200, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
        
        // Act
        strategyA.ApplyOperation(context);
        
        // Assert
        model.BestTime.ShouldBe(150);
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { BestTime = 150 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.bestTime", OperationType.Upsert, 100, timestampProvider.Create(2L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);
    
        // Act
        strategyA.ApplyOperation(context);
        var scoreAfterFirst = model.BestTime;
        strategyA.ApplyOperation(context);
    
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
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.bestTime", OperationType.Upsert, 200, timestampProvider.Create(2L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.bestTime", OperationType.Upsert, 250, timestampProvider.Create(3L));

        // Act
        // op1 then op2
        strategyA.ApplyOperation(new ApplyOperationContext(model1, new CrdtMetadata(), op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, new CrdtMetadata(), op2));

        // op2 then op1
        strategyA.ApplyOperation(new ApplyOperationContext(model2, new CrdtMetadata(), op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, new CrdtMetadata(), op1));
    
        // Assert
        model1.BestTime.ShouldBe(200); // min(300, 200, 250)
        model2.BestTime.ShouldBe(200);
        model1.BestTime.ShouldBe(model2.BestTime);
    }
    
    [Fact]
    public void ApplyOperation_IsAssociative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.bestTime", OperationType.Upsert, 200, timestampProvider.Create(2L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.bestTime", OperationType.Upsert, 250, timestampProvider.Create(3L));
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.bestTime", OperationType.Upsert, 150, timestampProvider.Create(4L));

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
                strategyA.ApplyOperation(new ApplyOperationContext(model, meta, op));
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