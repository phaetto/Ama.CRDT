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

public sealed class CounterMapTestPoco : IEquatable<CounterMapTestPoco>
{
    public Dictionary<string, decimal> Counters { get; set; } = new();

    public bool Equals(CounterMapTestPoco? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Counters.Count != other.Counters.Count) return false;
        foreach (var kvp in Counters)
        {
            if (!other.Counters.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as CounterMapTestPoco);
    
    public override int GetHashCode() => Counters.Count.GetHashCode();
}

public sealed class CounterMapStrategyProperties
{
    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        string key1, int inc1, 
        string key2, int inc2)
    {
        if (key1 is null || key2 is null) return;

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(CounterMapTestPoco.Counters),
            OperationType.Increment,
            new KeyValuePair<object, object?>(key1, (decimal)inc1),
            new EpochTimestamp(1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(CounterMapTestPoco.Counters),
            OperationType.Increment,
            new KeyValuePair<object, object?>(key2, (decimal)inc2),
            new EpochTimestamp(2),
            0);

        var stateAB = new CounterMapTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new CounterMapTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<string, int>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item1 != null).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(CounterMapTestPoco.Counters),
            OperationType.Increment,
            new KeyValuePair<object, object?>(x.Item1, (decimal)x.Item2),
            new EpochTimestamp(i),
            0)).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new CounterMapTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new CounterMapTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(CounterMapTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new CounterMapStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(CounterMapTestPoco).GetProperty(nameof(CounterMapTestPoco.Counters));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(CounterMapTestPoco.Counters)
            };
            strategy.ApplyOperation(context);
        }
    }
}