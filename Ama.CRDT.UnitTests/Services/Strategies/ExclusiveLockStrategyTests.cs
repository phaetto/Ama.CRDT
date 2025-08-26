namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class ExclusiveLockStrategyTests
{
    private sealed class TestModel
    {
        public string? UserId { get; set; }

        [CrdtExclusiveLockStrategy("$.userId")]
        public string? LockedValue { get; set; }
    }

    private readonly ExclusiveLockStrategy strategy;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();
    private readonly string replicaId = "replica-1";

    public ExclusiveLockStrategyTests()
    {
        strategy = new ExclusiveLockStrategy(Options.Create(new CrdtOptions { ReplicaId = replicaId }));
    }

    [Fact]
    public void GeneratePatch_WhenLockAcquiredAndValueChanged_ShouldGenerateUpsert()
    {
        // Arrange
        var original = new TestModel { UserId = null, LockedValue = "A" };
        var modified = new TestModel { UserId = "user1", LockedValue = "B" };
        var originalMeta = new CrdtMetadata { Lww = { ["$.lockedValue"] = new EpochTimestamp(100L) } };
        var modifiedMeta = new CrdtMetadata { Lww = { ["$.lockedValue"] = new EpochTimestamp(200L) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.LockedValue))!;

        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, "$.lockedValue", property, original.LockedValue, modified.LockedValue, original, modified, originalMeta, modifiedMeta);

        // Assert
        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.lockedValue");
        op.Timestamp.ShouldBe(new EpochTimestamp(200L));
        var payload = (ExclusiveLockPayload)op.Value!;
        payload.Value.ShouldBe("B");
        payload.LockHolderId.ShouldBe("user1");
    }

    [Fact]
    public void GeneratePatch_WhenLockIsHeldByOther_ShouldNotGeneratePatch()
    {
        // Arrange
        var original = new TestModel { UserId = "user1", LockedValue = "A" };
        var modified = new TestModel { UserId = "user2", LockedValue = "B" };
        var originalMeta = new CrdtMetadata
        {
            Lww = { ["$.lockedValue"] = new EpochTimestamp(100L) },
            ExclusiveLocks = { ["$.lockedValue"] = new LockInfo("user1", new EpochTimestamp(100L)) }
        };
        var modifiedMeta = new CrdtMetadata { Lww = { ["$.lockedValue"] = new EpochTimestamp(200L) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.LockedValue))!;

        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, "$.lockedValue", property, original.LockedValue, modified.LockedValue, original, modified, originalMeta, modifiedMeta);

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void ApplyOperation_WithNewerTimestamp_ShouldApplyChangeAndLock()
    {
        // Arrange
        var model = new TestModel { LockedValue = "A" };
        var metadata = new CrdtMetadata
        {
            ExclusiveLocks = { ["$.lockedValue"] = new LockInfo("user1", new EpochTimestamp(100L)) }
        };
        var payload = new ExclusiveLockPayload("B", "user2");
        var operation = new CrdtOperation(Guid.NewGuid(), "r2", "$.lockedValue", OperationType.Upsert, payload, new EpochTimestamp(200L));

        // Act
        strategy.ApplyOperation(model, metadata, operation);

        // Assert
        model.LockedValue.ShouldBe("B");
        var currentLock = metadata.ExclusiveLocks["$.lockedValue"];
        currentLock.ShouldNotBeNull();
        currentLock.LockHolderId.ShouldBe("user2");
        currentLock.Timestamp.ShouldBe(new EpochTimestamp(200L));
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { LockedValue = "A" };
        var metadata = new CrdtMetadata();
        var payload = new ExclusiveLockPayload("B", "user1");
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.lockedValue", OperationType.Upsert, payload, new EpochTimestamp(100L));

        // Act
        strategy.ApplyOperation(model, metadata, operation);
        var valueAfterFirstApply = model.LockedValue;
        var lockAfterFirstApply = metadata.ExclusiveLocks["$.lockedValue"];

        strategy.ApplyOperation(model, metadata, operation);
        
        // Assert
        model.LockedValue.ShouldBe(valueAfterFirstApply);
        metadata.ExclusiveLocks["$.lockedValue"].ShouldBe(lockAfterFirstApply);
    }
    
    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var payload1 = new ExclusiveLockPayload("B", "user1");
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.lockedValue", OperationType.Upsert, payload1, new EpochTimestamp(200L));

        var payload2 = new ExclusiveLockPayload("C", "user2");
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.lockedValue", OperationType.Upsert, payload2, new EpochTimestamp(300L));

        // Scenario 1: op1 then op2
        var model1 = new TestModel { LockedValue = "A" };
        var meta1 = new CrdtMetadata();
        strategy.ApplyOperation(model1, meta1, op1);
        strategy.ApplyOperation(model1, meta1, op2);

        // Scenario 2: op2 then op1
        var model2 = new TestModel { LockedValue = "A" };
        var meta2 = new CrdtMetadata();
        strategy.ApplyOperation(model2, meta2, op2);
        strategy.ApplyOperation(model2, meta2, op1);

        // Assert
        model1.LockedValue.ShouldBe("C");
        model2.LockedValue.ShouldBe("C");
        meta1.ExclusiveLocks["$.lockedValue"]?.LockHolderId.ShouldBe("user2");
        meta2.ExclusiveLocks["$.lockedValue"]?.LockHolderId.ShouldBe("user2");
    }

    [Fact]
    public void ApplyOperation_IsAssociative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.lockedValue", OperationType.Upsert, new ExclusiveLockPayload("B", "user1"), new EpochTimestamp(200L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.lockedValue", OperationType.Upsert, new ExclusiveLockPayload("C", "user2"), new EpochTimestamp(300L));
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.lockedValue", OperationType.Upsert, new ExclusiveLockPayload("D", "user3"), new EpochTimestamp(150L));

        var ops = new[] { op1, op2, op3 };
        var permutations = GetPermutations(ops, ops.Length);

        // Act & Assert
        foreach (var p in permutations)
        {
            var model = new TestModel { LockedValue = "A" };
            var meta = new CrdtMetadata();
            foreach (var op in p)
            {
                strategy.ApplyOperation(model, meta, op);
            }
            model.LockedValue.ShouldBe("C");
            meta.ExclusiveLocks["$.lockedValue"]?.LockHolderId.ShouldBe("user2");
        }
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