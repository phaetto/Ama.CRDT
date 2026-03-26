namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class FwwStrategyTests : IDisposable
{
    private sealed class TestModel { public int Value { get; set; } }
    
    private sealed class NullableTestModel { public int? Value { get; set; } }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly FwwStrategy strategyA;
    private readonly FwwStrategy strategyB;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    public FwwStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        strategyA = scopeA.ServiceProvider.GetRequiredService<FwwStrategy>();
        strategyB = scopeB.ServiceProvider.GetRequiredService<FwwStrategy>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }

    [Fact]
    public void GeneratePatch_WhenValueChanges_ShouldGenerateUpsert()
    {
        // Arrange
        var originalValue = 10;
        var modifiedValue = 20;
        var originalMeta = new CrdtMetadata { Fww = { ["$.value"] = new CausalTimestamp(timestampProvider.Create(300L), "r1", 1) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;
        var changeTimestamp = timestampProvider.Create(200L); // Older timestamp, should be accepted in FWW
        var context = new GeneratePatchContext(
            operations, new List<DifferentiateObjectContext>(), "$.value", property, originalValue, modifiedValue, null, null, originalMeta, changeTimestamp, 0);

        // Act
        strategyA.GeneratePatch(context);

        // Assert
        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.value");
        op.Value.ShouldBe(20);
        op.Timestamp.ShouldBe(changeTimestamp);
    }

    [Fact]
    public void GeneratePatch_WhenValueChangesButTimestampIsNewer_ShouldNotGenerateUpsert()
    {
        // Arrange
        var originalValue = 10;
        var modifiedValue = 20;
        var originalMeta = new CrdtMetadata { Fww = { ["$.value"] = new CausalTimestamp(timestampProvider.Create(100L), "r1", 1) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;
        var changeTimestamp = timestampProvider.Create(200L); // Newer timestamp, should be rejected in FWW
        var context = new GeneratePatchContext(
            operations, new List<DifferentiateObjectContext>(), "$.value", property, originalValue, modifiedValue, null, null, originalMeta, changeTimestamp, 0);

        // Act
        strategyA.GeneratePatch(context);

        // Assert
        operations.Count.ShouldBe(0);
    }
    
    [Fact]
    public void GenerateOperation_WithSetIntent_ShouldGenerateUpsert()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;
        var changeTimestamp = timestampProvider.Create(200L);
        var intent = new SetIntent(42);
        var context = new GenerateOperationContext(
            new TestModel(), new CrdtMetadata(), "$.Value", property, intent, changeTimestamp, 0);

        // Act
        var operation = strategyA.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.Value");
        operation.Value.ShouldBe(42);
        operation.Timestamp.ShouldBe(changeTimestamp);
        operation.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithNullSetIntent_ShouldGenerateRemove()
    {
        // Arrange
        var property = typeof(NullableTestModel).GetProperty(nameof(NullableTestModel.Value))!;
        var changeTimestamp = timestampProvider.Create(200L);
        var intent = new SetIntent(null);
        var context = new GenerateOperationContext(
            new NullableTestModel(), new CrdtMetadata(), "$.Value", property, intent, changeTimestamp, 0);

        // Act
        var operation = strategyA.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Remove);
        operation.JsonPath.ShouldBe("$.Value");
        operation.Value.ShouldBeNull();
        operation.Timestamp.ShouldBe(changeTimestamp);
        operation.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithClearIntent_ShouldGenerateRemove()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;
        var changeTimestamp = timestampProvider.Create(200L);
        var intent = new ClearIntent();
        var context = new GenerateOperationContext(
            new TestModel(), new CrdtMetadata(), "$.Value", property, intent, changeTimestamp, 0);

        // Act
        var operation = strategyA.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Remove);
        operation.JsonPath.ShouldBe("$.Value");
        operation.Value.ShouldBeNull();
        operation.Timestamp.ShouldBe(changeTimestamp);
        operation.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;
        var changeTimestamp = timestampProvider.Create(200L);
        var intent = new IncrementIntent(1);
        var context = new GenerateOperationContext(
            new TestModel(), new CrdtMetadata(), "$.Value", property, intent, changeTimestamp, 0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_Upsert_ShouldUpdateValue()
    {
        // Arrange
        var model = new TestModel { Value = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L), 0);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation);

        // Act
        strategyA.ApplyOperation(context);

        // Assert
        model.Value.ShouldBe(20);
    }

    [Fact]
    public void ApplyOperation_Reset_ShouldClearValueAndMetadata()
    {
        // Arrange
        var model = new NullableTestModel { Value = 10 };
        var metadata = new CrdtMetadata();
        metadata.Fww["$.Value"] = new CausalTimestamp(timestampProvider.Create(100L), "r1", 1);
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Remove, null, timestampProvider.Create(200L), 0);
        var context = new ApplyOperationContext(model, metadata, operation);
        
        // Act
        strategyA.ApplyOperation(context);

        // Assert
        model.Value.ShouldBeNull();
        metadata.Fww.ShouldNotContainKey("$.Value");
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { Value = 10 };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L), 0);
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategyA.ApplyOperation(context);
        var valueAfterFirstApply = model.Value;
        strategyA.ApplyOperation(context);

        // Assert
        model.Value.ShouldBe(valueAfterFirstApply);
        model.Value.ShouldBe(20);
        metadata.Fww["$.Value"].Timestamp.ShouldBe(timestampProvider.Create(200L));
    }

    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.Value", OperationType.Upsert, 30, timestampProvider.Create(300L), 0);

        // Scenario 1: op1 then op2
        var model1 = new TestModel { Value = 10 };
        var meta1 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op2));

        // Scenario 2: op2 then op1
        var model2 = new TestModel { Value = 10 };
        var meta2 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op1));

        // Assert: The lowest timestamp wins (op1 = 20), so the final state is deterministic and commutative.
        model1.Value.ShouldBe(20);
        model2.Value.ShouldBe(20);
    }
    
    [Fact]
    public void ApplyOperation_IsAssociative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.Value", OperationType.Upsert, 20, timestampProvider.Create(200L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.Value", OperationType.Upsert, 30, timestampProvider.Create(300L), 0);
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.Value", OperationType.Upsert, 15, timestampProvider.Create(150L), 0);

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
                strategyA.ApplyOperation(new ApplyOperationContext(model, meta, op));
            }
            finalValues.Add(model.Value);
        }

        // Assert
        // The lowest timestamp wins (op3 with value 15 and timestamp 150)
        finalValues.ShouldAllBe(v => v == 15);
    }

    [Fact]
    public void Compact_ShouldRemoveMetadata_WhenPolicyAllows()
    {
        // Arrange
        var mockPolicy = new Mock<ICompactionPolicy>();
        // Only compact candidates from "r1" with a version <= 5
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "r1" && c.Version <= 5))).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId != "r1" || c.Version > 5))).Returns(false);

        var metadata = new CrdtMetadata();
        metadata.Fww["$.Value"] = new CausalTimestamp(timestampProvider.Create(200L), "r1", 5);
        metadata.Fww["$.Value.Keep"] = new CausalTimestamp(timestampProvider.Create(300L), "r2", 6);

        var context = new CompactionContext(metadata, mockPolicy.Object, "Value", "$.Value", new TestModel());

        // Act
        strategyA.Compact(context);

        // Assert
        metadata.Fww.ShouldNotContainKey("$.Value"); // Removed as it satisfies the policy
        metadata.Fww.ShouldContainKey("$.Value.Keep"); // Kept
    }

    [Fact]
    public void Compact_ShouldNotRemoveMetadata_WhenPolicyDenies()
    {
        // Arrange
        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(false);
        var metadata = new CrdtMetadata();
        metadata.Fww["$.Value"] = new CausalTimestamp(timestampProvider.Create(200L), "r1", 5);

        var context = new CompactionContext(metadata, mockPolicy.Object, "Value", "$.Value", new TestModel());

        // Act
        strategyA.Compact(context);

        // Assert
        metadata.Fww.ShouldContainKey("$.Value");
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