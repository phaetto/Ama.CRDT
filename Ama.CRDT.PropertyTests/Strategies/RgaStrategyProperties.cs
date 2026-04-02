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

[CrdtSerializable(typeof(RgaTestPoco))]
[CrdtSerializable(typeof(List<string>))]
public partial class RgaTestContext : CrdtContext { }

public sealed class RgaTestPoco : IEquatable<RgaTestPoco>
{
    public List<string> Items { get; set; } = new();

    public bool Equals(RgaTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as RgaTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class RgaStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string item, bool isRemove)
    {
        if (item is null) return;

        var rgaId = new RgaIdentifier(timestamp, "replica-1");
        
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(RgaTestPoco.Items),
            isRemove ? OperationType.Remove : OperationType.Upsert,
            isRemove ? (object)rgaId : new RgaItem(rgaId, null, item, false),
            new EpochTimestamp(timestamp),
            0);

        var state1 = new RgaTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new RgaTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string item1, bool isRemove1, 
        long timestamp2, string item2, bool isRemove2)
    {
        if (item1 is null || item2 is null) return;
        if (timestamp1 == timestamp2) return; // RGA identifiers need strict distinct timestamps for deterministic tie-breaking on roots

        var rgaId1 = new RgaIdentifier(timestamp1, "replica-1");
        var rgaId2 = new RgaIdentifier(timestamp2, "replica-2");

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(RgaTestPoco.Items),
            isRemove1 ? OperationType.Remove : OperationType.Upsert,
            isRemove1 ? (object)rgaId1 : new RgaItem(rgaId1, null, item1, false),
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(RgaTestPoco.Items),
            isRemove2 ? OperationType.Remove : OperationType.Upsert,
            isRemove2 ? (object)rgaId2 : new RgaItem(rgaId2, null, item2, false),
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new RgaTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new RgaTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, bool>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var rgaId = new RgaIdentifier(x.Item1, $"replica-{i}");
            var isRemove = x.Item3;
            
            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                nameof(RgaTestPoco.Items),
                isRemove ? OperationType.Remove : OperationType.Upsert,
                isRemove ? (object)rgaId : new RgaItem(rgaId, null, x.Item2, false),
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new RgaTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new RgaTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(RgaTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var mockTimestampProvider = new Mock<ICrdtTimestampProvider>();
        mockTimestampProvider.Setup(x => x.Create(It.IsAny<long>())).Returns(new EpochTimestamp(0));

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var aotContexts = new CrdtContext[] { new RgaTestContext(), new InternalCrdtContext() };
        var strategy = new RgaStrategy(mockComparerProvider.Object, mockTimestampProvider.Object, replicaContext, aotContexts);
        
        var propertyInfo = new CrdtPropertyInfo(
            nameof(RgaTestPoco.Items),
            "items",
            typeof(List<string>),
            true,
            true,
            obj => ((RgaTestPoco)obj).Items,
            (obj, val) => ((RgaTestPoco)obj).Items = (List<string>)val!,
            null,
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(RgaTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}