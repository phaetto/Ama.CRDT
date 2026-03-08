namespace Ama.CRDT.PropertyTests.Strategies.Decorators;

using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Services.Strategies.Decorators;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public sealed class ApprovalQuorumTestPoco : IEquatable<ApprovalQuorumTestPoco>
{
    [CrdtApprovalQuorum(2)]
    public List<string> Items { get; set; } = new();

    public bool Equals(ApprovalQuorumTestPoco? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        // Using GSet internally for perfect order independence testing alongside quorums
        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj) => Equals(obj as ApprovalQuorumTestPoco);
    
    public override int GetHashCode() => Items.Count.GetHashCode();
}

public sealed class ApprovalQuorumStrategyProperties
{
    [Property]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, int, string>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item3 != null).ToList();
        if (opsData.Count == 0) return;

        // Modulo replication ID by 3 to simulate multiple replicas repeatedly voting
        var ops = opsData.Select(x => new CrdtOperation(
            Guid.NewGuid(),
            $"replica-{Math.Abs(x.Item2) % 3}",
            nameof(ApprovalQuorumTestPoco.Items),
            OperationType.Upsert,
            new QuorumPayload(x.Item3),
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new ApprovalQuorumTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new ApprovalQuorumTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(ApprovalQuorumTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var innerStrategy = new GSetStrategy(mockComparerProvider.Object, new ReplicaContext { ReplicaId = "inner-replica" });

        var mockStrategyProvider = new Mock<ICrdtStrategyProvider>();
        mockStrategyProvider
            .Setup(x => x.GetInnerStrategy(It.IsAny<PropertyInfo>(), typeof(ApprovalQuorumStrategy)))
            .Returns(innerStrategy);

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(ICrdtStrategyProvider))).Returns(mockStrategyProvider.Object);

        var strategy = new ApprovalQuorumStrategy(mockServiceProvider.Object, mockComparerProvider.Object);
        var propertyInfo = typeof(ApprovalQuorumTestPoco).GetProperty(nameof(ApprovalQuorumTestPoco.Items));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(ApprovalQuorumTestPoco.Items)
            };
            strategy.ApplyOperation(context);
        }
    }
}