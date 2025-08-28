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

public sealed class LwwStrategyTests
{
    private sealed class TestModel { public int Value { get; set; } }

    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();
    private readonly string replicaId = Guid.NewGuid().ToString();

    public LwwStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, SequentialTimestampProvider>();
        serviceProvider = services.BuildServiceProvider();
        timestampProvider = serviceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    [Fact]
    public void GeneratePatch_WhenValueChanges_ShouldGenerateUpsert()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<LwwStrategy>();

        var originalValue = 10;
        var modifiedValue = 20;
        var originalMeta = new CrdtMetadata { Lww = { ["$.value"] = timestampProvider.Create(100L) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;
        var changeTimestamp = timestampProvider.Create(200L);
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, null, null, originalMeta, changeTimestamp);

        // Act
        strategy.GeneratePatch(context);

        // Assert
        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.value");
        op.Value.ShouldBe(20);
        op.Timestamp.ShouldBe(changeTimestamp);
    }

    [Fact]
    public void ApplyOperation_Upsert_ShouldUpdateValue()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<LwwStrategy>();

        var model = new TestModel { Value = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L));
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act
        strategy.ApplyOperation(context);

        // Assert
        model.Value.ShouldBe(20);
    }

    [Fact]
    public void ApplyOperation_Remove_ShouldSetValueToNull()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<LwwStrategy>();

        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Remove, null, timestampProvider.Create(200L));
        
        // This test is for when the property is nullable. For non-nullable value types, it will set to default.
        // Let's test a nullable property.
        var nullableModel = new NullableTestModel { Value = 10 };
        var context = new ApplyOperationContext(nullableModel, new CrdtMetadata(), operation);
        
        // Act
        strategy.ApplyOperation(context);

        // Assert
        nullableModel.Value.ShouldBeNull();
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<LwwStrategy>();

        var model = new TestModel { Value = 10 };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategy.ApplyOperation(context);
        var valueAfterFirstApply = model.Value;
        strategy.ApplyOperation(context);

        // Assert
        model.Value.ShouldBe(valueAfterFirstApply);
        model.Value.ShouldBe(20);
        metadata.Lww["$.Value"].ShouldBe(timestampProvider.Create(200L));
    }

    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<LwwStrategy>();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.Value", OperationType.Upsert, 30, timestampProvider.Create(300L));

        // Scenario 1: op1 then op2
        var model1 = new TestModel { Value = 10 };
        var meta1 = new CrdtMetadata();
        strategy.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategy.ApplyOperation(new ApplyOperationContext(model1, meta1, op2));

        // Scenario 2: op2 then op1
        var model2 = new TestModel { Value = 10 };
        var meta2 = new CrdtMetadata();
        strategy.ApplyOperation(new ApplyOperationContext(model2, meta2, op2));
        strategy.ApplyOperation(new ApplyOperationContext(model2, meta2, op1));

        // Assert: The highest timestamp wins, so the final state is deterministic and commutative.
        model1.Value.ShouldBe(30);
        model2.Value.ShouldBe(30);
    }
    
    [Fact]
    public void ApplyOperation_IsAssociative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<LwwStrategy>();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.Value", OperationType.Upsert, 30, timestampProvider.Create(300L));
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.Value", OperationType.Upsert, 15, timestampProvider.Create(150L));

        var ops = new[] { op1, op2, op3 };
        var permutations = GetPermutations(ops, ops.Length);
        var finalValues = new List<int>();
        
        // Act
        foreach (var permutation in permutations)
        {
            var model = new TestModel { Value = 10 };
            var meta = new CrdtMetadata();
            foreach (var op in permutation)
            {
                strategy.ApplyOperation(new ApplyOperationContext(model, meta, op));
            }
            finalValues.Add(model.Value);
        }

        // Assert
        // The highest timestamp wins (op2 with value 30)
        finalValues.ShouldAllBe(v => v == 30);
    }

    private sealed class NullableTestModel { public int? Value { get; set; } }
    
    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
}