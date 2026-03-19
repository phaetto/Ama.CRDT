namespace Ama.CRDT.PropertyTests.Strategies;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using FsCheck;
using Moq;
using Shouldly;

public sealed class LseqTestPoco : IEquatable<LseqTestPoco>
{
    public List<string> Items { get; set; } = new();

    public bool Equals(LseqTestPoco? other)
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

    public override bool Equals(object? obj) => Equals(obj as LseqTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class LseqStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(int pos, string? val)
    {
        var position = Math.Abs(pos) + 1;
        var identifier = new LseqIdentifier(ImmutableList.Create(new LseqPathSegment(position, "replica-1")));

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(LseqTestPoco.Items),
            OperationType.Upsert,
            new LseqItem(identifier, val ?? string.Empty),
            new EpochTimestamp(1),
            0);

        var state1 = new LseqTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new LseqTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        int pos1, string? val1,
        int pos2, string? val2)
    {
        var position1 = Math.Abs(pos1) + 1;
        var position2 = Math.Abs(pos2) + 1;

        var id1 = new LseqIdentifier(ImmutableList.Create(new LseqPathSegment(position1, "replica-1")));
        var id2 = new LseqIdentifier(ImmutableList.Create(new LseqPathSegment(position2, "replica-2")));

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(LseqTestPoco.Items),
            OperationType.Upsert,
            new LseqItem(id1, val1 ?? string.Empty),
            new EpochTimestamp(1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(LseqTestPoco.Items),
            OperationType.Upsert,
            new LseqItem(id2, val2 ?? string.Empty),
            new EpochTimestamp(2),
            0);

        var stateAB = new LseqTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new LseqTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<bool, int, string?>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0)
        {
            return;
        }

        var ops = rawOps.Select((x, i) =>
        {
            var isUpsert = x.Item1;
            var posInt = Math.Abs(x.Item2) + 1;
            var val = x.Item3 ?? string.Empty;
            
            var opId = Guid.NewGuid();
            var identifier = new LseqIdentifier(ImmutableList.Create(new LseqPathSegment(posInt, $"replica-{i}")));

            if (isUpsert)
            {
                return new CrdtOperation(
                    opId,
                    $"replica-{i}",
                    nameof(LseqTestPoco.Items),
                    OperationType.Upsert,
                    new LseqItem(identifier, val),
                    new EpochTimestamp(i),
                    0);
            }
            else
            {
                return new CrdtOperation(
                    opId,
                    $"replica-{i}",
                    nameof(LseqTestPoco.Items),
                    OperationType.Remove,
                    identifier,
                    new EpochTimestamp(i),
                    0);
            }
        }).ToList();

        var random = new System.Random(rawOps.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new LseqTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new LseqTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(LseqTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var mockTimestampProvider = new Mock<ICrdtTimestampProvider>();
        mockTimestampProvider.Setup(x => x.Create(It.IsAny<long>())).Returns(new EpochTimestamp(0));

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new LseqStrategy(mockComparerProvider.Object, mockTimestampProvider.Object, replicaContext);
        var propertyInfo = typeof(LseqTestPoco).GetProperty(nameof(LseqTestPoco.Items));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(LseqTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}