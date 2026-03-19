namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using FsCheck;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public sealed class ReplicatedTreeTestPoco
{
    public CrdtTree Tree { get; set; } = new();
}

public sealed class ReplicatedTreeStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string nodeId, string parentId, string value, Guid tag)
    {
        if (nodeId is null) return;

        object payload = new TreeMoveNodePayload(nodeId, parentId);

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(ReplicatedTreeTestPoco.Tree),
            OperationType.Upsert,
            payload,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new ReplicatedTreeTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new ReplicatedTreeTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        Serialize(state1).ShouldBe(Serialize(state2));
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string nodeId1, string parentId1,
        long timestamp2, string nodeId2, string parentId2)
    {
        if (nodeId1 is null || nodeId2 is null) return;
        if (timestamp1 == timestamp2) return; // Need strict inequality due to LWW logic resolving parent moves

        // Restricting properties tests strictly to Move operations to isolate Tree commutativity correctly
        // Combining Adds and Moves concurrently onto the exact same randomly generated `nodeId` string creates an invalid causality paradox
        object payload1 = new TreeMoveNodePayload(nodeId1, parentId1);
        object payload2 = new TreeMoveNodePayload(nodeId2, parentId2);

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(ReplicatedTreeTestPoco.Tree),
            OperationType.Upsert,
            payload1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(ReplicatedTreeTestPoco.Tree),
            OperationType.Upsert,
            payload2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new ReplicatedTreeTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new ReplicatedTreeTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        Serialize(stateAB).ShouldBe(Serialize(stateBA));
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, string>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).DistinctBy(x => x.Item1).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var timestamp = x.Item1;
            var nodeId = x.Item2;
            var parentId = x.Item3;

            object payload = new TreeMoveNodePayload(nodeId, parentId);

            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                nameof(ReplicatedTreeTestPoco.Tree),
                OperationType.Upsert,
                payload,
                new EpochTimestamp(timestamp),
                0);
        }).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new ReplicatedTreeTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new ReplicatedTreeTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        Serialize(state1).ShouldBe(Serialize(state2));
    }

    private static void ApplyOperations(ReplicatedTreeTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new ReplicatedTreeStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(ReplicatedTreeTestPoco).GetProperty(nameof(ReplicatedTreeTestPoco.Tree));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(ReplicatedTreeTestPoco.Tree)
            };
            strategy.ApplyOperation(context);
        }
    }

    private static string Serialize(ReplicatedTreeTestPoco state)
    {
        var normalized = new
        {
            Tree = new
            {
                Nodes = state.Tree.Nodes
                    .OrderBy(kvp => kvp.Key)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            }
        };
        return JsonSerializer.Serialize(normalized);
    }
}