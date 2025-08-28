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

public sealed class AverageRegisterStrategyTests
{
    private sealed class TestModel { public decimal Rating { get; set; } }

    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private const string Path = "$.rating";

    public AverageRegisterStrategyTests()
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
        using var scope = scopeFactory.CreateScope("r1");
        var strategy = scope.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();

        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Rating))!;
        var originalRoot = new TestModel { Rating = 3.5m };
        var modifiedRoot = new TestModel { Rating = 4.0m };
        var context = new GeneratePatchContext(
            new Mock<ICrdtPatcher>().Object,
            operations,
            Path,
            property,
            3.5m,
            4.0m,
            originalRoot,
            modifiedRoot,
            new CrdtMetadata(),
            timestampProvider.Create(1L)
        );
        
        // Act
        strategy.GeneratePatch(context);

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(4.0m);
    }
    
    [Fact]
    public void ApplyOperation_ShouldCalculateCorrectAverage()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("r1");
        var strategy = scope.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();

        var model = new TestModel();
        var metadata = new CrdtMetadata();
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L));
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op2));
        
        // Assert
        model.Rating.ShouldBe(7.5m); // (5 + 10) / 2
    }
    
    [Fact]
    public void ApplyOperation_ShouldBeIdempotent()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("r1");
        var strategy = scope.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();

        var model = new TestModel();
        var metadata = new CrdtMetadata();
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L));
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op2));
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op2)); // Apply second op again
        
        // Assert
        model.Rating.ShouldBe(7.5m);
        metadata.AverageRegisters[Path].Count.ShouldBe(2);
    }
    
    [Fact]
    public void ApplyOperation_ShouldBeCommutative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("r1");
        var strategy = scope.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();

        var model1 = new TestModel();
        var metadata1 = new CrdtMetadata();
        var model2 = new TestModel();
        var metadata2 = new CrdtMetadata();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L));
        
        // Act: Apply in different orders
        strategy.ApplyOperation(new ApplyOperationContext(model1, metadata1, op1));
        strategy.ApplyOperation(new ApplyOperationContext(model1, metadata1, op2));
        
        strategy.ApplyOperation(new ApplyOperationContext(model2, metadata2, op2));
        strategy.ApplyOperation(new ApplyOperationContext(model2, metadata2, op1));

        // Assert
        model1.Rating.ShouldBe(7.5m);
        model2.Rating.ShouldBe(7.5m);
        model1.Rating.ShouldBe(model2.Rating);
    }
    
    [Fact]
    public void ApplyOperation_ShouldBeAssociative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("r1");
        var strategy = scope.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L));
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", Path, OperationType.Upsert, 15m, timestampProvider.Create(3L));

        var operations = new[] { op1, op2, op3 };
        var permutations = GetPermutations(operations, 3);
        var finalStates = new List<decimal>();

        // Act
        foreach(var p in permutations)
        {
            var model = new TestModel();
            var meta = new CrdtMetadata();
            foreach(var op in p)
            {
                strategy.ApplyOperation(new ApplyOperationContext(model, meta, op));
            }
            finalStates.Add(model.Rating);
        }

        // Assert
        var expectedAverage = (5m + 10m + 15m) / 3;
        finalStates.ShouldAllBe(s => s == expectedAverage);
        finalStates.Count.ShouldBe(6);
    }

    [Fact]
    public void ApplyOperation_ShouldUseLastWriteWins_ForSameReplica()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("r1");
        var strategy = scope.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();

        var model = new TestModel();
        var metadata = new CrdtMetadata();
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 8m, timestampProvider.Create(2L)); // Newer timestamp
        var op3 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 3m, timestampProvider.Create(1L)); // Older timestamp
        
        // Act
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op1));
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op2));
        strategy.ApplyOperation(new ApplyOperationContext(model, metadata, op3)); // This one should be ignored
        
        // Assert
        model.Rating.ShouldBe(8m); // Only one value from r1
        metadata.AverageRegisters[Path].Count.ShouldBe(1);
        metadata.AverageRegisters[Path]["r1"].Value.ShouldBe(8m);
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