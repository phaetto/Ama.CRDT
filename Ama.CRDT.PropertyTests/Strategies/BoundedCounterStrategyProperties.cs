namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

[CrdtAotType(typeof(BoundedCounterTestPoco))]
public partial class BoundedCounterTestContext : CrdtAotContext
{
}

public sealed class BoundedCounterTestPoco : IEquatable<BoundedCounterTestPoco>
{
    [CrdtBoundedCounterStrategy(-50, 50)]
    public decimal Value { get; set; }

    public bool Equals(BoundedCounterTestPoco? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as BoundedCounterTestPoco);
    
    public override int GetHashCode() => Value.GetHashCode();
}

public sealed class BoundedCounterStrategyProperties
{
    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(int inc1, int inc2)
    {
        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(BoundedCounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc1,
            new EpochTimestamp(1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(BoundedCounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc2,
            new EpochTimestamp(2),
            0);

        var stateAB = new BoundedCounterTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new BoundedCounterTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<int> increments)
    {
        if (increments is null || increments.Count == 0)
        {
            return;
        }

        var ops = increments.Select((inc, i) => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{i}",
            nameof(BoundedCounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc,
            new EpochTimestamp(i),
            0)).ToList();

        var random = new Random(increments.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new BoundedCounterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new BoundedCounterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(BoundedCounterTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<BoundedCounterTestContext>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("property-test-replica");
        var strategy = scope.ServiceProvider.GetRequiredService<BoundedCounterStrategy>();

        var propertyInfo = new CrdtPropertyInfo(
            nameof(BoundedCounterTestPoco.Value),
            "value",
            typeof(decimal),
            true,
            true,
            obj => ((BoundedCounterTestPoco)obj).Value,
            (obj, val) => ((BoundedCounterTestPoco)obj).Value = (decimal)val!,
            new CrdtBoundedCounterStrategyAttribute(-50, 50),
            []);

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(BoundedCounterTestPoco.Value)
            };
            strategy.ApplyOperation(context);
        }
    }
}