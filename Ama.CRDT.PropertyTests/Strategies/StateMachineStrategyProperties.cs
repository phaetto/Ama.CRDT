namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AlwaysTrueMockValidator : IStateMachine<int>
{
    // For pure CRDT commutativity testing without arbitrary application logic falsely rejecting branches based on ordering.
    // LWW layer is purely verified to converge the state.
    public bool IsValidTransition(int currentState, int nextState) => true;
}

public sealed class StateMachineTestPoco : IEquatable<StateMachineTestPoco>
{
    [CrdtStateMachineStrategy(typeof(AlwaysTrueMockValidator))]
    public int State { get; set; }

    public bool Equals(StateMachineTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return State == other.State;
    }

    public override bool Equals(object? obj) => Equals(obj as StateMachineTestPoco);
    
    public override int GetHashCode() => State.GetHashCode();
}

public sealed class StateMachineStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, int value)
    {
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(StateMachineTestPoco.State),
            OperationType.Upsert,
            value,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new StateMachineTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new StateMachineTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, int value1, 
        long timestamp2, int value2)
    {
        if (timestamp1 == timestamp2) return; // Strict inequality needed for true LWW commutativity

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(StateMachineTestPoco.State),
            OperationType.Upsert,
            value1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(StateMachineTestPoco.State),
            OperationType.Upsert,
            value2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new StateMachineTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new StateMachineTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, int>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(StateMachineTestPoco.State),
            OperationType.Upsert,
            x.Item2,
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new StateMachineTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new StateMachineTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(StateMachineTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(AlwaysTrueMockValidator))).Returns(new AlwaysTrueMockValidator());

        var strategy = new StateMachineStrategy(replicaContext, mockServiceProvider.Object);
        var propertyInfo = typeof(StateMachineTestPoco).GetProperty(nameof(StateMachineTestPoco.State));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(StateMachineTestPoco.State)
            };
            strategy.ApplyOperation(context);
        }
    }
}