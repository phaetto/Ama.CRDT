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
using System.Text.Json;

public sealed class TwoPhaseGraphTestPoco
{
    public CrdtGraph Graph { get; set; } = new();
}

public sealed class TwoPhaseGraphStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string vertex1, string vertex2, bool isEdge, bool isRemove)
    {
        if (vertex1 is null || vertex2 is null) return;

        object payload = isEdge 
            ? new GraphEdgePayload(new Edge(vertex1, vertex2, null)) 
            : new GraphVertexPayload(vertex1);

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(TwoPhaseGraphTestPoco.Graph),
            isRemove ? OperationType.Remove : OperationType.Upsert,
            payload,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new TwoPhaseGraphTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new TwoPhaseGraphTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        Serialize(state1).ShouldBe(Serialize(state2));
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string v1a, string v1b, bool isEdge1, bool isRemove1,
        long timestamp2, string v2a, string v2b, bool isEdge2, bool isRemove2)
    {
        if (v1a is null || v1b is null || v2a is null || v2b is null) return;

        object payload1 = isEdge1 ? new GraphEdgePayload(new Edge(v1a, v1b, null)) : new GraphVertexPayload(v1a);
        object payload2 = isEdge2 ? new GraphEdgePayload(new Edge(v2a, v2b, null)) : new GraphVertexPayload(v2a);

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(TwoPhaseGraphTestPoco.Graph),
            isRemove1 ? OperationType.Remove : OperationType.Upsert,
            payload1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(TwoPhaseGraphTestPoco.Graph),
            isRemove2 ? OperationType.Remove : OperationType.Upsert,
            payload2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new TwoPhaseGraphTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new TwoPhaseGraphTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        Serialize(stateAB).ShouldBe(Serialize(stateBA));
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string, string, bool, bool>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null && x.Item3 != null).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var isEdge = x.Item4;
            var isRemove = x.Item5;

            object payload = isEdge 
                ? new GraphEdgePayload(new Edge(x.Item2, x.Item3, null)) 
                : new GraphVertexPayload(x.Item2);

            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                nameof(TwoPhaseGraphTestPoco.Graph),
                isRemove ? OperationType.Remove : OperationType.Upsert,
                payload,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new System.Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new TwoPhaseGraphTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new TwoPhaseGraphTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        Serialize(state1).ShouldBe(Serialize(state2));
    }

    private static void ApplyOperations(TwoPhaseGraphTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var mockComparerProvider = new Mock<IElementComparerProvider>();
        mockComparerProvider
            .Setup(x => x.GetComparer(It.IsAny<Type>()))
            .Returns(EqualityComparer<object>.Default);

        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new TwoPhaseGraphStrategy(mockComparerProvider.Object, replicaContext);
        var propertyInfo = typeof(TwoPhaseGraphTestPoco).GetProperty(nameof(TwoPhaseGraphTestPoco.Graph));

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(TwoPhaseGraphTestPoco.Graph)
            };
            strategy.ApplyOperation(context);
        }
    }

    private static string Serialize(TwoPhaseGraphTestPoco state)
    {
        var normalized = new
        {
            Graph = new
            {
                // Force Ordinal comparison and convert object to string explicitly to satisfy the compiler
                Vertices = state.Graph.Vertices.OrderBy(v => v?.ToString(), StringComparer.Ordinal).ToList(),
                Edges = state.Graph.Edges.OrderBy(e => e.Source?.ToString(), StringComparer.Ordinal).ThenBy(e => e.Target?.ToString(), StringComparer.Ordinal).ToList()
            }
        };
        return JsonSerializer.Serialize(normalized);
    }
}