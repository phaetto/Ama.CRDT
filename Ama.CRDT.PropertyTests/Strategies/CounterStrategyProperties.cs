namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CounterTestPoco : IEquatable<CounterTestPoco>
{
    public decimal Value { get; set; }

    public bool Equals(CounterTestPoco? other)
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

    public override bool Equals(object? obj) => Equals(obj as CounterTestPoco);
    
    public override int GetHashCode() => Value.GetHashCode();
}

[CrdtAotType(typeof(CounterTestPoco))]
internal partial class CounterTestContext : CrdtAotContext { }

public sealed class CounterStrategyProperties : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly ICrdtScopeFactory scopeFactory;

    public CounterStrategyProperties()
    {
        serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<CounterTestContext>()
            .BuildServiceProvider();

        scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
    }

    public void Dispose()
    {
        serviceProvider.Dispose();
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(int inc1, int inc2)
    {
        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(CounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc1,
            new EpochTimestamp(1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(CounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc2,
            new EpochTimestamp(2),
            0);

        var stateAB = new CounterTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new CounterTestPoco();
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
            nameof(CounterTestPoco.Value),
            OperationType.Increment,
            (decimal)inc,
            new EpochTimestamp(i),
            0)).ToList();

        var random = new Random(increments.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new CounterTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new CounterTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private void ApplyOperations(CounterTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        using var scope = scopeFactory.CreateScope("property-test-replica");
        var strategy = scope.ServiceProvider.GetRequiredService<CounterStrategy>();
        var aotContexts = scope.ServiceProvider.GetRequiredService<IEnumerable<CrdtAotContext>>();
        
        var propertyInfo = PocoPathHelper.GetTypeInfo(typeof(CounterTestPoco), aotContexts)
            .Properties.First(p => p.Value.Name == nameof(CounterTestPoco.Value));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo.Value,
                FinalSegment = nameof(CounterTestPoco.Value)
            };
            strategy.ApplyOperation(context);
        }
    }
}