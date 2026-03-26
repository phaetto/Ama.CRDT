namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using System;
using System.Linq;

[CrdtSupportedType(typeof(CrdtGraph))]
[CrdtSupportedIntent(typeof(AddVertexIntent))]
[CrdtSupportedIntent(typeof(AddEdgeIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class GraphStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalGraph = originalValue as CrdtGraph ?? new CrdtGraph();
        var modifiedGraph = modifiedValue as CrdtGraph ?? new CrdtGraph();
        
        foreach (var vertex in modifiedGraph.Vertices.Except(originalGraph.Vertices))
        {
            var payload = new GraphVertexPayload(vertex);
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp, clock));
        }

        foreach (var edge in modifiedGraph.Edges.Except(originalGraph.Edges))
        {
            var payload = new GraphEdgePayload(edge);
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp, clock));
        }
    }

    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (_, _, jsonPath, _, intent, timestamp, clock) = context;

        if (intent is AddVertexIntent addVertexIntent)
        {
            var payload = new GraphVertexPayload(addVertexIntent.Vertex);
            return new CrdtOperation(Guid.NewGuid(), replicaId, jsonPath, OperationType.Upsert, payload, timestamp, clock);
        }

        if (intent is AddEdgeIntent addEdgeIntent)
        {
            var payload = new GraphEdgePayload(addEdgeIntent.Edge);
            return new CrdtOperation(Guid.NewGuid(), replicaId, jsonPath, OperationType.Upsert, payload, timestamp, clock);
        }

        throw new NotSupportedException($"The intent '{intent.GetType().Name}' is not supported by {nameof(GraphStrategy)}.");
    }

    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, _, operation) = context;
        
        var graphObj = PocoPathHelper.GetValue(root, operation.JsonPath);
        if (graphObj is not CrdtGraph graph)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        if (operation.Value is GraphVertexPayload vertexPayload)
        {
            graph.Vertices.Add(vertexPayload.Vertex);
        }
        else if (operation.Value is GraphEdgePayload edgePayload)
        {
            graph.Edges.Add(edgePayload.Edge);
        }
        else
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        return CrdtOperationStatus.Success;
    }

    public void Compact(CompactionContext context)
    {
        // GraphStrategy is a grow-only implementation and does not track tombstones or use metadata.
        // Therefore, there is no state to prune.
    }
}