namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AverageRegisterTestPoco : IEquatable<AverageRegisterTestPoco>
{
    public decimal Value { get; set; }

    public bool Equals(AverageRegisterTestPoco? other)
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

    public override bool Equals(object? obj) => Equals(obj as AverageRegisterTestPoco);
    
    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString("G");
}

public sealed class AverageRegisterStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, decimal value)
    {
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(AverageRegisterTestPoco.Value),
            OperationType.Upsert,
            value,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new AverageRegisterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new AverageRegisterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(long timestamp1, decimal value1, long timestamp2, decimal value2)
    {
        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(AverageRegisterTestPoco.Value),
            OperationType.Upsert,
            value1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(AverageRegisterTestPoco.Value),
            OperationType.Upsert,
            value2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new AverageRegisterTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new AverageRegisterTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, decimal>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0)
        {
            return;
        }

        // Each operation gets a unique replica ID to guarantee they all contribute to the average correctly
        // and aren't discarded as obsolete tie-breaks from the same replica.
        var ops = rawOps.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(AverageRegisterTestPoco.Value),
            OperationType.Upsert,
            x.Item2,
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new System.Random(rawOps.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new AverageRegisterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new AverageRegisterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(AverageRegisterTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new AverageRegisterStrategy(replicaContext);
        var propertyInfo = typeof(AverageRegisterTestPoco).GetProperty(nameof(AverageRegisterTestPoco.Value));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(AverageRegisterTestPoco.Value)
            };
            strategy.ApplyOperation(context);
        }
    }
}