namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FixedSizeArrayTestPoco : IEquatable<FixedSizeArrayTestPoco>
{
    [CrdtFixedSizeArrayStrategy(5)]
    public List<string?> Items { get; set; } = new();

    public bool Equals(FixedSizeArrayTestPoco? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as FixedSizeArrayTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class FixedSizeArrayStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, int rawIndex, string? value)
    {
        var index = Math.Abs(rawIndex) % 5;
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            $"{nameof(FixedSizeArrayTestPoco.Items)}[{index}]",
            OperationType.Upsert,
            value,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new FixedSizeArrayTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new FixedSizeArrayTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, int rawIndex1, string? value1, 
        long timestamp2, int rawIndex2, string? value2)
    {
        if (timestamp1 == timestamp2) return; // LWW strict inequality prevents ordering flakiness on conflicts

        var idx1 = Math.Abs(rawIndex1) % 5;
        var idx2 = Math.Abs(rawIndex2) % 5;

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            $"{nameof(FixedSizeArrayTestPoco.Items)}[{idx1}]",
            OperationType.Upsert,
            value1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            $"{nameof(FixedSizeArrayTestPoco.Items)}[{idx2}]",
            OperationType.Upsert,
            value2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new FixedSizeArrayTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new FixedSizeArrayTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, int, string?>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var distinctOpsData = rawOps.DistinctBy(x => x.Item1).ToList();
        if (distinctOpsData.Count == 0) return;

        var ops = distinctOpsData.Select(x => {
            var index = Math.Abs(x.Item2) % 5;
            return new CrdtOperation(
                Guid.NewGuid(),
                "replica-1",
                $"{nameof(FixedSizeArrayTestPoco.Items)}[{index}]",
                OperationType.Upsert,
                x.Item3,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new System.Random(distinctOpsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new FixedSizeArrayTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new FixedSizeArrayTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(FixedSizeArrayTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new FixedSizeArrayStrategy(replicaContext);
        var propertyInfo = typeof(FixedSizeArrayTestPoco).GetProperty(nameof(FixedSizeArrayTestPoco.Items));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(FixedSizeArrayTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}