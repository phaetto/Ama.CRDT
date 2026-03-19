namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using FsCheck;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CounterTestPoco : IEquatable<CounterTestPoco>
{
    public decimal Value { get; set; }

    public bool Equals(CounterTestPoco? other)
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

    public override bool Equals(object? obj) => Equals(obj as CounterTestPoco);
    
    public override int GetHashCode() => Value.GetHashCode();
}

public sealed class CounterStrategyProperties
{
    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(int inc1, int inc2)
    {
        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(CounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc1,
            new EpochTimestamp(1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(CounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc2,
            new EpochTimestamp(2),
            0);

        var stateAB = new CounterTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new CounterTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<int> increments)
    {
        if (increments is null || increments.Count == 0)
        {
            return;
        }

        var ops = increments.Select((inc, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(CounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc,
            new EpochTimestamp(i),
            0)).ToList();

        var random = new System.Random(increments.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new CounterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new CounterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(CounterTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new CounterStrategy(replicaContext);
        var propertyInfo = typeof(CounterTestPoco).GetProperty(nameof(CounterTestPoco.Value));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(CounterTestPoco.Value)
            };
            strategy.ApplyOperation(context);
        }
    }
}