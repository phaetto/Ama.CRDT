namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using FsCheck;
using FsCheck.Xunit;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class MinWinsTestPoco : IEquatable<MinWinsTestPoco>
{
    public int? Value { get; set; }

    public bool Equals(MinWinsTestPoco? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as MinWinsTestPoco);
    
    public override int GetHashCode() => Value.GetHashCode();
}

public sealed class MinWinsStrategyProperties
{
    [Property]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, int value)
    {
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(MinWinsTestPoco.Value),
            OperationType.Upsert,
            value,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new MinWinsTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new MinWinsTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [Property]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(long timestamp1, int value1, long timestamp2, int value2)
    {
        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(MinWinsTestPoco.Value),
            OperationType.Upsert,
            value1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(MinWinsTestPoco.Value),
            OperationType.Upsert,
            value2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new MinWinsTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new MinWinsTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [Property]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, int>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0)
        {
            return;
        }

        var ops = rawOps.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(MinWinsTestPoco.Value),
            OperationType.Upsert,
            x.Item2,
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new System.Random(rawOps.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new MinWinsTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new MinWinsTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(MinWinsTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new MinWinsStrategy(replicaContext);
        var propertyInfo = typeof(MinWinsTestPoco).GetProperty(nameof(MinWinsTestPoco.Value));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(MinWinsTestPoco.Value)
            };
            strategy.ApplyOperation(context);
        }
    }
}