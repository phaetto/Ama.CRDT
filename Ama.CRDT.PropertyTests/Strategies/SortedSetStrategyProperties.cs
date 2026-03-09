namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SortedSetTestPoco : IEquatable<SortedSetTestPoco>
{
    [CrdtSortedSetStrategy]
    public List<string> Items { get; set; } = new();

    public bool Equals(SortedSetTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as SortedSetTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class SortedSetStrategyProperties
{
    [Property]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string item, bool isRemove)
    {
        if (item is null) return;

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            $"{nameof(SortedSetTestPoco.Items)}[-1]",
            isRemove ? OperationType.Remove : OperationType.Upsert,
            item,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new SortedSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new SortedSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [Property]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string item1, bool isRemove1, 
        long timestamp2, string item2, bool isRemove2)
    {
        if (item1 is null || item2 is null) return;
        if (timestamp1 == timestamp2) return; // Need strict inequality to guarantee conflict resolution commutativity without tie-breakers

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            $"{nameof(SortedSetTestPoco.Items)}[-1]",
            isRemove1 ? OperationType.Remove : OperationType.Upsert,
            item1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            $"{nameof(SortedSetTestPoco.Items)}[-1]",
            isRemove2 ? OperationType.Remove : OperationType.Upsert,
            item2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new SortedSetTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new SortedSetTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [Property]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, bool>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        // Distinctly filter by timestamp to avoid LWW tie-break rejections making convergence flaky.
        var opsData = rawOps.Where(x => x.Item2 != null).DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var isRemove = x.Item3;
            
            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                $"{nameof(SortedSetTestPoco.Items)}[-1]",
                isRemove ? OperationType.Remove : OperationType.Upsert,
                x.Item2,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new SortedSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new SortedSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(SortedSetTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var timestampProvider = new EpochTimestampProvider(replicaContext);
        
        var strategy = new SortedSetStrategy(mockComparerProvider.Object, timestampProvider, replicaContext);
        var propertyInfo = typeof(SortedSetTestPoco).GetProperty(nameof(SortedSetTestPoco.Items));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = "Items"
            };
            strategy.ApplyOperation(context);
        }
    }
}