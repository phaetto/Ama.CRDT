namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes.Strategies;
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

public sealed class PqItem : IEquatable<PqItem>
{
    public string Id { get; set; } = string.Empty;
    public int Priority { get; set; }

    public bool Equals(PqItem? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id; // Only compare ID to allow priority updates seamlessly
    }

    public override bool Equals(object? obj) => Equals(obj as PqItem);
    
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;

    public override string ToString() => $"[{Id}:{Priority}]";
}

public sealed class PriorityQueueTestPoco : IEquatable<PriorityQueueTestPoco>
{
    [CrdtPriorityQueueStrategy(nameof(PqItem.Priority))]
    public List<PqItem> Queue { get; set; } = new();

    public bool Equals(PriorityQueueTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Queue.Count != other.Queue.Count) return false;
        
        // Items with exact same priority must be stably sorted by an additional trait to ensure List sequences are identical.
        // We MUST use StringComparer.Ordinal for strings so control characters and nulls do not map to the same sort weight
        var mySorted = Queue.OrderByDescending(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
        var otherSorted = other.Queue.OrderByDescending(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
        
        for (int i = 0; i < mySorted.Count; i++)
        {
            if (mySorted[i].Id != otherSorted[i].Id || mySorted[i].Priority != otherSorted[i].Priority) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as PriorityQueueTestPoco);
    
    public override int GetHashCode() => Queue.Count.GetHashCode();

    public override string ToString() => string.Join(", ", Queue);
}

public sealed class PriorityQueueStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string id, int priority, bool isRemove)
    {
        if (id is null) return;

        var item = new PqItem { Id = id, Priority = priority };
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(PriorityQueueTestPoco.Queue),
            isRemove ? OperationType.Remove : OperationType.Upsert,
            item,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new PriorityQueueTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new PriorityQueueTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string id1, int priority1, bool isRemove1,
        long timestamp2, string id2, int priority2, bool isRemove2)
    {
        if (id1 is null || id2 is null) return;
        if (timestamp1 == timestamp2) return; // Strict inequality needed to prevent flakiness in LWW based priority overrides

        var item1 = new PqItem { Id = id1, Priority = priority1 };
        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(PriorityQueueTestPoco.Queue),
            isRemove1 ? OperationType.Remove : OperationType.Upsert,
            item1,
            new EpochTimestamp(timestamp1),
            0);

        var item2 = new PqItem { Id = id2, Priority = priority2 };
        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(PriorityQueueTestPoco.Queue),
            isRemove2 ? OperationType.Remove : OperationType.Upsert,
            item2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new PriorityQueueTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new PriorityQueueTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, int, bool>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var item = new PqItem { Id = x.Item2, Priority = x.Item3 };
            var isRemove = x.Item4;

            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                nameof(PriorityQueueTestPoco.Queue),
                isRemove ? OperationType.Remove : OperationType.Upsert,
                item,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new PriorityQueueTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new PriorityQueueTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(PriorityQueueTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new PriorityQueueStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(PriorityQueueTestPoco).GetProperty(nameof(PriorityQueueTestPoco.Queue));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(PriorityQueueTestPoco.Queue)
            };
            strategy.ApplyOperation(context);
        }
    }
}