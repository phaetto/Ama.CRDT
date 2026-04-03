namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

[CrdtAotType(typeof(OrSetTestPoco))]
[CrdtAotType(typeof(List<string>))]
public partial class OrSetTestContext : CrdtAotContext { }

public sealed class OrSetTestPoco : IEquatable<OrSetTestPoco>
{
    public List<string> Items { get; set; } = new();

    public bool Equals(OrSetTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        // OrSetStrategy maintains elements internally sorted natively for determinism
        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as OrSetTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class OrSetStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string item, bool isRemove, Guid tag)
    {
        if (item is null) return;

        object payload = isRemove 
            ? new OrSetRemoveItem(item, new HashSet<Guid> { tag }) 
            : new OrSetAddItem(item, tag);

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(OrSetTestPoco.Items),
            isRemove ? OperationType.Remove : OperationType.Upsert,
            payload,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new OrSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new OrSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string item1, bool isRemove1, Guid tag1,
        long timestamp2, string item2, bool isRemove2, Guid tag2)
    {
        if (item1 is null || item2 is null) return;

        object payload1 = isRemove1 
            ? new OrSetRemoveItem(item1, new HashSet<Guid> { tag1 }) 
            : new OrSetAddItem(item1, tag1);

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(OrSetTestPoco.Items),
            isRemove1 ? OperationType.Remove : OperationType.Upsert,
            payload1,
            new EpochTimestamp(timestamp1),
            0);

        object payload2 = isRemove2 
            ? new OrSetRemoveItem(item2, new HashSet<Guid> { tag2 }) 
            : new OrSetAddItem(item2, tag2);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(OrSetTestPoco.Items),
            isRemove2 ? OperationType.Remove : OperationType.Upsert,
            payload2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new OrSetTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new OrSetTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, bool, Guid>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var isRemove = x.Item3;
            object payload = isRemove 
                ? new OrSetRemoveItem(x.Item2, new HashSet<Guid> { x.Item4 }) 
                : new OrSetAddItem(x.Item2, x.Item4);

            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                nameof(OrSetTestPoco.Items),
                isRemove ? OperationType.Remove : OperationType.Upsert,
                payload,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new OrSetTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new OrSetTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(OrSetTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var aotContexts = new CrdtAotContext[] { new OrSetTestContext(), new InternalCrdtAotContext() };
        var strategy = new OrSetStrategy(mockComparerProvider.Object, replicaContext, aotContexts);
        
        var propertyInfo = new CrdtPropertyInfo(
            nameof(OrSetTestPoco.Items),
            "items",
            typeof(List<string>),
            true,
            true,
            obj => ((OrSetTestPoco)obj).Items,
            (obj, val) => ((OrSetTestPoco)obj).Items = (List<string>)val!,
            null,
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(OrSetTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}