namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Attributes;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public sealed class GraphTestPoco
{
    public CrdtGraph Graph { get; set; } = new();
}

[CrdtAotType(typeof(GraphTestPoco))]
internal partial class GraphTestContext : CrdtAotContext
{
}

public sealed class GraphStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string vertex)
    {
        if (vertex is null) return;

        var payload = new GraphVertexPayload(vertex);

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(GraphTestPoco.Graph),
            OperationType.Upsert,
            payload,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new GraphTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new GraphTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        Serialize(state1).ShouldBe(Serialize(state2));
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(
        long timestamp1, string vertex1, 
        long timestamp2, string vertex2)
    {
        if (vertex1 is null || vertex2 is null) return;

        var payload1 = new GraphVertexPayload(vertex1);
        var payload2 = new GraphVertexPayload(vertex2);

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(GraphTestPoco.Graph),
            OperationType.Upsert,
            payload1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(GraphTestPoco.Graph),
            OperationType.Upsert,
            payload2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new GraphTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new GraphTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        Serialize(stateAB).ShouldBe(Serialize(stateBA));
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0) return;

        var opsData = rawOps.Where(x => x.Item2 != null).ToList();
        if (opsData.Count == 0) return;

        var ops = opsData.Select((x, i) => {
            var payload = new GraphVertexPayload(x.Item2);
            return new CrdtOperation(
                Guid.NewGuid(),
                $"replica-{i}",
                nameof(GraphTestPoco.Graph),
                OperationType.Upsert,
                payload,
                new EpochTimestamp(x.Item1),
                0);
        }).ToList();

        var random = new Random(opsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new GraphTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new GraphTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        Serialize(state1).ShouldBe(Serialize(state2));
    }

    private static void ApplyOperations(GraphTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new GraphStrategy(replicaContext);
        
        var propertyInfo = new CrdtPropertyInfo(
            name: nameof(GraphTestPoco.Graph),
            jsonName: "graph",
            propertyType: typeof(CrdtGraph),
            canRead: true,
            canWrite: true,
            getter: obj => ((GraphTestPoco)obj).Graph,
            setter: (obj, val) => ((GraphTestPoco)obj).Graph = (CrdtGraph)val!,
            strategyAttribute: null,
            decoratorAttributes: Array.Empty<CrdtStrategyDecoratorAttribute>()
        );

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(GraphTestPoco.Graph)
            };
            strategy.ApplyOperation(context);
        }
    }

    private static string Serialize(GraphTestPoco state)
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