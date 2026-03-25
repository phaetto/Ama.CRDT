namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class MinWinsMapTestPoco : IEquatable<MinWinsMapTestPoco>
{
    public Dictionary<string, int> Map { get; set; } = new();

    public bool Equals(MinWinsMapTestPoco? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Map.Count != other.Map.Count) return false;
        foreach (var kvp in Map)
        {
            if (!other.Map.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as MinWinsMapTestPoco);
    
    public override int GetHashCode() => Map.Count.GetHashCode();
}

public sealed class MinWinsMapStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string key, int value)
    {
        if (key is null) return;

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(MinWinsMapTestPoco.Map),
            OperationType.Upsert,
            new KeyValuePair<object, object?>(key, value),
            new EpochTimestamp(timestamp),
            0);

        var state1 = new MinWinsMapTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new MinWinsMapTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string key1, int value1, 
        long timestamp2, string key2, int value2)
    {
        if (key1 is null || key2 is null) return;

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(MinWinsMapTestPoco.Map),
            OperationType.Upsert,
            new KeyValuePair<object, object?>(key1, value1),
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(MinWinsMapTestPoco.Map),
            OperationType.Upsert,
            new KeyValuePair<object, object?>(key2, value2),
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new MinWinsMapTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new MinWinsMapTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, int>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(MinWinsMapTestPoco.Map),
            OperationType.Upsert,
            new KeyValuePair<object, object?>(x.Item2, x.Item3),
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new MinWinsMapTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new MinWinsMapTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(MinWinsMapTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new MinWinsMapStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(MinWinsMapTestPoco).GetProperty(nameof(MinWinsMapTestPoco.Map));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(MinWinsMapTestPoco.Map)
            };
            strategy.ApplyOperation(context);
        }
    }
}