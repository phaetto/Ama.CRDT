namespace Ama.CRDT.PropertyTests.Strategies;

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

public sealed class GSetTestPoco : IEquatable<GSetTestPoco>
{
    public List<string> Items { get; set; } = new();

    public bool Equals(GSetTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        // GSetStrategy sorts the items alphabetically after every add to ensure total ordering,
        // so SequenceEqual is a robust structural equality check.
        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as GSetTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class GSetStrategyProperties
{
    [Property]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string item)
    {
        if (item is null) return;

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(GSetTestPoco.Items),
            OperationType.Upsert,
            item,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new GSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new GSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [Property]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string item1, 
        long timestamp2, string item2)
    {
        if (item1 is null || item2 is null) return;

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(GSetTestPoco.Items),
            OperationType.Upsert,
            item1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(GSetTestPoco.Items),
            OperationType.Upsert,
            item2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new GSetTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new GSetTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [Property]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(GSetTestPoco.Items),
            OperationType.Upsert,
            x.Item2,
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new GSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new GSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(GSetTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new GSetStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(GSetTestPoco).GetProperty(nameof(GSetTestPoco.Items));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(GSetTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}