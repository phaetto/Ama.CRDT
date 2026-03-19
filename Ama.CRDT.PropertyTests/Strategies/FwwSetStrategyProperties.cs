namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using FsCheck;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FwwSetTestPoco : IEquatable<FwwSetTestPoco>
{
    public List<string> Items { get; set; } = new();

    public bool Equals(FwwSetTestPoco? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Items.Count != other.Items.Count) return false;
        var thisSet = new HashSet<string>(Items);
        return thisSet.SetEquals(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as FwwSetTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class FwwSetStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string item, bool isRemove)
    {
        if (item is null) return;

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(FwwSetTestPoco.Items),
            isRemove ? OperationType.Remove : OperationType.Upsert,
            item,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new FwwSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new FwwSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string item1, bool isRemove1, 
        long timestamp2, string item2, bool isRemove2)
    {
        if (item1 is null || item2 is null) return;
        if (timestamp1 == timestamp2) return; // Strict inequality needed for true FWW commutativity

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(FwwSetTestPoco.Items),
            isRemove1 ? OperationType.Remove : OperationType.Upsert,
            item1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(FwwSetTestPoco.Items),
            isRemove2 ? OperationType.Remove : OperationType.Upsert,
            item2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new FwwSetTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new FwwSetTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, bool>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select(x => {
            var isRemove = x.Item3;
            return new CrdtOperation(
                Guid.NewGuid(),
                "replica-1",
                nameof(FwwSetTestPoco.Items),
                isRemove ? OperationType.Remove : OperationType.Upsert,
                x.Item2,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new FwwSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new FwwSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(FwwSetTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var mockTimestampProvider = new Mock<ICrdtTimestampProvider>();
        mockTimestampProvider.Setup(x => x.Create(It.IsAny<long>())).Returns(new EpochTimestamp(0));

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new FwwSetStrategy(mockComparerProvider.Object, mockTimestampProvider.Object, replicaContext);
        var propertyInfo = typeof(FwwSetTestPoco).GetProperty(nameof(FwwSetTestPoco.Items));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(FwwSetTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}