namespace Ama.CRDT.PropertyTests.Strategies;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Moq;
using Shouldly;

public sealed class ArrayLcsTestPoco : IEquatable<ArrayLcsTestPoco>
{
    public List<string> Items { get; set; } = new();

    public bool Equals(ArrayLcsTestPoco? other)
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

    public override bool Equals(object? obj) => Equals(obj as ArrayLcsTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class ArrayLcsStrategyProperties
{
    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        int pos1, string? val1,
        int pos2, string? val2)
    {
        var position1 = (Math.Abs(pos1) + 1m).ToString("G29", CultureInfo.InvariantCulture);
        var position2 = (Math.Abs(pos2) + 1m).ToString("G29", CultureInfo.InvariantCulture);

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(ArrayLcsTestPoco.Items),
            OperationType.Upsert,
            new PositionalItem(position1, val1 ?? string.Empty),
            new EpochTimestamp(1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(ArrayLcsTestPoco.Items),
            OperationType.Upsert,
            new PositionalItem(position2, val2 ?? string.Empty),
            new EpochTimestamp(2),
            0);

        var stateAB = new ArrayLcsTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new ArrayLcsTestPoco();
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
            var posInt = x.Item2;
            var val = x.Item3 ?? string.Empty;
            
            var opId = Guid.NewGuid();
            var position = (Math.Abs(posInt) + 1m).ToString("G29", CultureInfo.InvariantCulture);

            if (isUpsert)
            {
                return new CrdtOperation(
                    opId,
                    $"replica-{i}",
                    nameof(ArrayLcsTestPoco.Items),
                    OperationType.Upsert,
                    new PositionalItem(position, val),
                    new EpochTimestamp(i),
                    0);
            }
            else
            {
                return new CrdtOperation(
                    opId,
                    $"replica-{i}",
                    nameof(ArrayLcsTestPoco.Items),
                    OperationType.Remove,
                    new PositionalIdentifier(position, opId),
                    new EpochTimestamp(i),
                    0);
            }
        }).ToList();

        var random = new Random(rawOps.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new ArrayLcsTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new ArrayLcsTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(ArrayLcsTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new ArrayLcsStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(ArrayLcsTestPoco).GetProperty(nameof(ArrayLcsTestPoco.Items));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(ArrayLcsTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}