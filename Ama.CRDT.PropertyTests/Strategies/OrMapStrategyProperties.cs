namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
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

public sealed class OrMapTestPoco : IEquatable<OrMapTestPoco>
{
    public Dictionary<string, string> Map { get; set; } = new();

    public bool Equals(OrMapTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Map.Count != other.Map.Count) return false;
        foreach (var kvp in Map)
        {
            if (!other.Map.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as OrMapTestPoco);
    
    public override int GetHashCode() => Map.Count.GetHashCode();
}

public sealed class OrMapStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string key, string value, bool isRemove, Guid tag)
    {
        if (key is null) return;

        object payload = isRemove 
            ? new OrMapRemoveItem(key, new HashSet<Guid> { tag }) 
            : new OrMapAddItem(key, value, tag);

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(OrMapTestPoco.Map),
            isRemove ? OperationType.Remove : OperationType.Upsert,
            payload,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new OrMapTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new OrMapTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string key1, string value1, bool isRemove1, Guid tag1,
        long timestamp2, string key2, string value2, bool isRemove2, Guid tag2)
    {
        if (key1 is null || key2 is null) return;
        if (timestamp1 == timestamp2) return; // Strict inequality for LWW value updates to resolve cleanly

        object payload1 = isRemove1 
            ? new OrMapRemoveItem(key1, new HashSet<Guid> { tag1 }) 
            : new OrMapAddItem(key1, value1, tag1);

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(OrMapTestPoco.Map),
            isRemove1 ? OperationType.Remove : OperationType.Upsert,
            payload1,
            new EpochTimestamp(timestamp1),
            0);

        object payload2 = isRemove2 
            ? new OrMapRemoveItem(key2, new HashSet<Guid> { tag2 }) 
            : new OrMapAddItem(key2, value2, tag2);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(OrMapTestPoco.Map),
            isRemove2 ? OperationType.Remove : OperationType.Upsert,
            payload2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new OrMapTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new OrMapTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, string, bool, Guid>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var isRemove = x.Item4;
            object payload = isRemove 
                ? new OrMapRemoveItem(x.Item2, new HashSet<Guid> { x.Item5 }) 
                : new OrMapAddItem(x.Item2, x.Item3, x.Item5);

            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                nameof(OrMapTestPoco.Map),
                isRemove ? OperationType.Remove : OperationType.Upsert,
                payload,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new OrMapTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new OrMapTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(OrMapTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new OrMapStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(OrMapTestPoco).GetProperty(nameof(OrMapTestPoco.Map));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(OrMapTestPoco.Map)
            };
            strategy.ApplyOperation(context);
        }
    }
}