namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GCounterTestPoco : IEquatable<GCounterTestPoco>
{
    public decimal Value { get; set; }

    public bool Equals(GCounterTestPoco? other)
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

    public override bool Equals(object? obj) => Equals(obj as GCounterTestPoco);
    
    public override int GetHashCode() => Value.GetHashCode();
}

public sealed class GCounterStrategyProperties
{
    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(int rawInc1, int rawInc2)
    {
        var inc1 = (decimal)Math.Abs(rawInc1) + 1m; // G-Counter only allows positive increments
        var inc2 = (decimal)Math.Abs(rawInc2) + 1m;

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(GCounterTestPoco.Value),
            OperationType.Increment,
            inc1,
            new EpochTimestamp(1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(GCounterTestPoco.Value),
            OperationType.Increment,
            inc2,
            new EpochTimestamp(2),
            0);

        var stateAB = new GCounterTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new GCounterTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<int> rawIncrements)
    {
        if (rawIncrements is null || rawIncrements.Count == 0)
        {
            return;
        }

        var ops = rawIncrements.Select((inc, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(GCounterTestPoco.Value),
            OperationType.Increment,
            (decimal)Math.Abs(inc) + 1m,
            new EpochTimestamp(i),
            0)).ToList();

        var random = new System.Random(rawIncrements.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new GCounterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new GCounterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(GCounterTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new GCounterStrategy(replicaContext);
        var propertyInfo = typeof(GCounterTestPoco).GetProperty(nameof(GCounterTestPoco.Value));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(GCounterTestPoco.Value)
            };
            strategy.ApplyOperation(context);
        }
    }
}