namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

public sealed class LwwStrategyTests
{
    private sealed class TestModel { public int Value { get; set; } }

    private readonly LwwStrategy strategy;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();

    public LwwStrategyTests()
    {
        strategy = new LwwStrategy(Options.Create(new CrdtOptions { ReplicaId = Guid.NewGuid().ToString() }));
    }

    [Fact]
    public void GeneratePatch_WhenModifiedIsNewer_ShouldGenerateUpsert()
    {
        var originalValue = 10;
        var modifiedValue = 20;
        var originalMeta = new CrdtMetadata { Lww = { ["$.value"] = new EpochTimestamp(100L) } };
        var modifiedMeta = new CrdtMetadata { Lww = { ["$.value"] = new EpochTimestamp(200L) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.value");
        op.Value.ShouldBe(20);
        op.Timestamp.ShouldBe(new EpochTimestamp(200L));
    }
    
    [Fact]
    public void GeneratePatch_WhenModifiedIsOlder_ShouldGenerateNothing()
    {
        var originalValue = 10;
        var modifiedValue = 20;
        var originalMeta = new CrdtMetadata { Lww = { ["$.value"] = new EpochTimestamp(200L) } };
        var modifiedMeta = new CrdtMetadata { Lww = { ["$.value"] = new EpochTimestamp(100L) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Value))!;

        strategy.GeneratePatch(mockPatcher.Object, operations, "$.value", property, originalValue, modifiedValue, originalMeta, modifiedMeta);

        operations.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyOperation_Upsert_ShouldUpdateValue()
    {
        // Arrange
        var model = new TestModel { Value = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Upsert, 20, new EpochTimestamp(200L));

        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);

        // Assert
        model.Value.ShouldBe(20);
    }

    [Fact]
    public void ApplyOperation_Remove_ShouldSetValueToNull()
    {
        // Arrange
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Remove, null, new EpochTimestamp(200L));
        
        // This test is for when the property is nullable. For non-nullable value types, it will set to default.
        // Let's test a nullable property.
        var nullableModel = new NullableTestModel { Value = 10 };
        
        // Act
        strategy.ApplyOperation(nullableModel, new CrdtMetadata(), operation);

        // Assert
        nullableModel.Value.ShouldBeNull();
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { Value = 10 };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Value", OperationType.Upsert, 20, new EpochTimestamp(200L));

        // Act
        strategy.ApplyOperation(model, metadata, operation);
        var valueAfterFirstApply = model.Value;
        strategy.ApplyOperation(model, metadata, operation);

        // Assert
        model.Value.ShouldBe(valueAfterFirstApply);
        model.Value.ShouldBe(20);
        metadata.Lww["$.Value"].ShouldBe(new EpochTimestamp(200L));
    }

    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.Value", OperationType.Upsert, 20, new EpochTimestamp(200L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.Value", OperationType.Upsert, 30, new EpochTimestamp(300L));

        // Scenario 1: op1 then op2
        var model1 = new TestModel { Value = 10 };
        var meta1 = new CrdtMetadata();
        strategy.ApplyOperation(model1, meta1, op1);
        strategy.ApplyOperation(model1, meta1, op2);

        // Scenario 2: op2 then op1
        var model2 = new TestModel { Value = 10 };
        var meta2 = new CrdtMetadata();
        strategy.ApplyOperation(model2, meta2, op2);
        strategy.ApplyOperation(model2, meta2, op1);

        // Assert: The highest timestamp wins, so the final state is deterministic and commutative.
        model1.Value.ShouldBe(30);
        model2.Value.ShouldBe(30);
    }

    private sealed class NullableTestModel { public int? Value { get; set; } }
}