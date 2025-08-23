namespace Modern.CRDT.UnitTests.Services.Strategies;

using Microsoft.Extensions.Options;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Reflection;
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
        strategy.ApplyOperation(model, operation);

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
        strategy.ApplyOperation(nullableModel, operation);

        // Assert
        nullableModel.Value.ShouldBeNull();
    }

    private sealed class NullableTestModel { public int? Value { get; set; } }
}