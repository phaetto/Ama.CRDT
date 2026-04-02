namespace Ama.CRDT.PropertyTests.Strategies.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Services.Strategies.Decorators;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class EpochBoundTestPoco : IEquatable<EpochBoundTestPoco>
{
    [CrdtEpochBound]
    public string? Value { get; set; }

    public bool Equals(EpochBoundTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as EpochBoundTestPoco);
    
    public override int GetHashCode() => Value?.GetHashCode(StringComparison.Ordinal) ?? 0;
}

[CrdtSerializable(typeof(EpochBoundTestPoco))]
public partial class EpochBoundTestContext : CrdtContext
{
}

public sealed class EpochBoundStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(int epoch, long timestamp, string? value, bool isClear)
    {
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(EpochBoundTestPoco.Value),
            isClear ? OperationType.Remove : OperationType.Upsert,
            new EpochPayload(epoch, isClear ? null : value),
            new EpochTimestamp(timestamp),
            0);

        var state1 = new EpochBoundTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new EpochBoundTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        int epoch1, long ts1, string? val1, bool clear1,
        int epoch2, long ts2, string? val2, bool clear2)
    {
        if (ts1 == ts2) return; // Strict inequality needed for deterministic fallback to inner LWW

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(EpochBoundTestPoco.Value),
            clear1 ? OperationType.Remove : OperationType.Upsert,
            new EpochPayload(epoch1, clear1 ? null : val1),
            new EpochTimestamp(ts1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(EpochBoundTestPoco.Value),
            clear2 ? OperationType.Remove : OperationType.Upsert,
            new EpochPayload(epoch2, clear2 ? null : val2),
            new EpochTimestamp(ts2),
            0);

        var stateAB = new EpochBoundTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new EpochBoundTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<int, long, string?, bool>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        // Distinct by timestamp to securely rely on inner LWW resolution during identical epochs
        var opsData = rawOps.DistinctBy(x => x.Item2).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(EpochBoundTestPoco.Value),
            x.Item4 ? OperationType.Remove : OperationType.Upsert,
            new EpochPayload(x.Item1, x.Item4 ? null : x.Item3),
            new EpochTimestamp(x.Item2),
            0)).ToList();

        var random = new Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new EpochBoundTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new EpochBoundTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(EpochBoundTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "inner-replica" };
        var innerStrategy = new LwwStrategy(replicaContext, [ new EpochBoundTestContext() ]);

        var mockStrategyProvider = new Mock<ICrdtStrategyProvider>();
        mockStrategyProvider
            .Setup(x => x.GetInnerStrategy(It.IsAny<Type>(), It.IsAny<CrdtPropertyInfo>(), typeof(EpochBoundStrategy)))
            .Returns(innerStrategy);

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(ICrdtStrategyProvider))).Returns(mockStrategyProvider.Object);

        var strategy = new EpochBoundStrategy(mockServiceProvider.Object, replicaContext, [new EpochBoundTestContext()]);
        var propertyInfo = new CrdtPropertyInfo(
            nameof(EpochBoundTestPoco.Value),
            "value",
            typeof(string),
            true,
            true,
            obj => ((EpochBoundTestPoco)obj).Value,
            (obj, val) => ((EpochBoundTestPoco)obj).Value = (string?)val,
            null,
            [new CrdtEpochBoundAttribute()]);

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(EpochBoundTestPoco.Value)
            };
            strategy.ApplyOperation(context);
        }
    }
}