namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class VoteCounterTestPoco : IEquatable<VoteCounterTestPoco>
{
    public Dictionary<string, List<string>> Votes { get; set; } = new();

    public bool Equals(VoteCounterTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Votes.Count != other.Votes.Count) return false;

        foreach (var kvp in Votes)
        {
            if (!other.Votes.TryGetValue(kvp.Key, out var otherList)) return false;
            if (kvp.Value.Count != otherList.Count) return false;
            
            var set1 = new HashSet<string>(kvp.Value);
            if (!set1.SetEquals(otherList)) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as VoteCounterTestPoco);
    
    public override int GetHashCode() => Votes.Count.GetHashCode();
}

public sealed class VoteCounterStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string voter, string option)
    {
        if (voter is null || option is null) return;

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(VoteCounterTestPoco.Votes),
            OperationType.Upsert,
            new VotePayload(voter, option),
            new EpochTimestamp(timestamp),
            0);

        var state1 = new VoteCounterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new VoteCounterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string voter1, string option1,
        long timestamp2, string voter2, string option2)
    {
        if (voter1 is null || option1 is null || voter2 is null || option2 is null) return;
        if (timestamp1 == timestamp2) return; // Strict inequality needed for true LWW commutativity tie breaks

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(VoteCounterTestPoco.Votes),
            OperationType.Upsert,
            new VotePayload(voter1, option1),
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(VoteCounterTestPoco.Votes),
            OperationType.Upsert,
            new VotePayload(voter2, option2),
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new VoteCounterTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new VoteCounterTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, string>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        // Distinctly filter by timestamp to avoid LWW tie-break rejections making convergence flaky
        var opsData = rawOps.Where(x => x.Item2 != null && x.Item3 != null).DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(VoteCounterTestPoco.Votes),
            OperationType.Upsert,
            new VotePayload(x.Item2, x.Item3),
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new VoteCounterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new VoteCounterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(VoteCounterTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new VoteCounterStrategy(replicaContext);
        var propertyInfo = typeof(VoteCounterTestPoco).GetProperty(nameof(VoteCounterTestPoco.Votes));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(VoteCounterTestPoco.Votes)
            };
            strategy.ApplyOperation(context);
        }
    }
}