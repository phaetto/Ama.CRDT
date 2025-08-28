namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
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

public sealed class ExclusiveLockStrategyTests
{
    private sealed class TestModel
    {
        public string? UserId { get; set; }

        [CrdtExclusiveLockStrategy("$.userId")]
        public string? LockedValue { get; set; }
    }

    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();
    private readonly string replicaId = "replica-1";

    public ExclusiveLockStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, SequentialTimestampProvider>();
        serviceProvider = services.BuildServiceProvider();
        timestampProvider = serviceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    [Fact]
    public void GeneratePatch_WhenLockAcquiredAndValueChanged_ShouldGenerateUpsert()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<ExclusiveLockStrategy>();

        var original = new TestModel { UserId = null, LockedValue = "A" };
        var modified = new TestModel { UserId = "user1", LockedValue = "B" };
        var originalMeta = new CrdtMetadata { Lww = { ["$.lockedValue"] = timestampProvider.Create(100L) } };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.LockedValue))!;
        var changeTimestamp = timestampProvider.Create(200L);
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.lockedValue", property, original.LockedValue, modified.LockedValue, original, modified, originalMeta, changeTimestamp);

        // Act
        strategy.GeneratePatch(context);

        // Assert
        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.lockedValue");
        op.Timestamp.ShouldBe(changeTimestamp);
        var payload = (ExclusiveLockPayload)op.Value!;
        payload.Value.ShouldBe("B");
        payload.LockHolderId.ShouldBe("user1");
    }

    [Fact]
    public void GeneratePatch_WhenLockIsHeldByOther_ShouldNotGeneratePatch()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<ExclusiveLockStrategy>();

        var original = new TestModel { UserId = "user1", LockedValue = "A" };
        var modified = new TestModel { UserId = "user2", LockedValue = "B" };
        var originalMeta = new CrdtMetadata
        {
            Lww = { ["$.lockedValue"] = timestampProvider.Create(100L) },
            ExclusiveLocks = { ["$.lockedValue"] = new LockInfo("user1", timestampProvider.Create(100L)) }
        };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.LockedValue))!;
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.lockedValue", property, original.LockedValue, modified.LockedValue, original, modified, originalMeta, timestampProvider.Create(200L));

        // Act
        strategy.GeneratePatch(context);

        // Assert
        operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void ApplyOperation_WithNewerTimestamp_ShouldApplyChangeAndLock()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<ExclusiveLockStrategy>();

        var model = new TestModel { LockedValue = "A" };
        var metadata = new CrdtMetadata
        {
            ExclusiveLocks = { ["$.lockedValue"] = new LockInfo("user1", timestampProvider.Create(100L)) }
        };
        var payload = new ExclusiveLockPayload("B", "user2");
        var operation = new CrdtOperation(Guid.NewGuid(), "r2", "$.lockedValue", OperationType.Upsert, payload, timestampProvider.Create(200L));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategy.ApplyOperation(context);

        // Assert
        model.LockedValue.ShouldBe("B");
        var currentLock = metadata.ExclusiveLocks["$.lockedValue"];
        currentLock.ShouldNotBeNull();
        currentLock.LockHolderId.ShouldBe("user2");
        currentLock.Timestamp.ShouldBe(timestampProvider.Create(200L));
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<ExclusiveLockStrategy>();

        var model = new TestModel { LockedValue = "A" };
        var metadata = new CrdtMetadata();
        var payload = new ExclusiveLockPayload("B", "user1");
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.lockedValue", OperationType.Upsert, payload, timestampProvider.Create(100L));
        var context = new ApplyOperationContext(model, metadata, operation);

        // Act
        strategy.ApplyOperation(context);
        var valueAfterFirstApply = model.LockedValue;
        var lockAfterFirstApply = metadata.ExclusiveLocks["$.lockedValue"];

        strategy.ApplyOperation(context);
        
        // Assert
        model.LockedValue.ShouldBe(valueAfterFirstApply);
        metadata.ExclusiveLocks["$.lockedValue"].ShouldBe(lockAfterFirstApply);
    }
    
    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        // Arrange
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<ExclusiveLockStrategy>();

        var payload1 = new ExclusiveLockPayload("B", "user1");
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.lockedValue", OperationType.Upsert, payload1, timestampProvider.Create(200L));

        var payload2 = new ExclusiveLockPayload("C", "user2");
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.lockedValue", OperationType.Upsert, payload2, timestampProvider.Create(300L));

        // Scenario 1: op1 then op2
        var model1 = new TestModel { LockedValue = "A" };
        var meta1 = new CrdtMetadata();
        strategy.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategy.ApplyOperation(new ApplyOperationContext(model1, meta1, op2));

        // Scenario 2: op2 then op1
        var model2 = new TestModel { LockedValue = "A" };
        var meta2 = new CrdtMetadata();
        strategy.ApplyOperation(new ApplyOperationContext(model2, meta2, op2));
        strategy.ApplyOperation(new ApplyOperationContext(model2, meta2, op1));

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
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope(replicaId);
        var strategy = scope.ServiceProvider.GetRequiredService<ExclusiveLockStrategy>();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.lockedValue", OperationType.Upsert, new ExclusiveLockPayload("B", "user1"), timestampProvider.Create(200L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.lockedValue", OperationType.Upsert, new ExclusiveLockPayload("C", "user2"), timestampProvider.Create(300L));
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", "$.lockedValue", OperationType.Upsert, new ExclusiveLockPayload("D", "user3"), timestampProvider.Create(150L));

        var ops = new[] { op1, op2, op3 };
        var permutations = GetPermutations(ops, ops.Length);

        // Act & Assert
        foreach (var p in permutations)
        {
            var model = new TestModel { LockedValue = "A" };
            var meta = new CrdtMetadata();
            foreach (var op in p)
            {
                strategy.ApplyOperation(new ApplyOperationContext(model, meta, op));
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