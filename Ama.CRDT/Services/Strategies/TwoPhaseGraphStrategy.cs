namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections.Generic;
using System.Linq;

[CrdtSupportedType(typeof(CrdtGraph))]
[CrdtSupportedIntent(typeof(AddVertexIntent))]
[CrdtSupportedIntent(typeof(RemoveVertexIntent))]
[CrdtSupportedIntent(typeof(AddEdgeIntent))]
[CrdtSupportedIntent(typeof(RemoveEdgeIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class TwoPhaseGraphStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        if (originalValue is not CrdtGraph originalGraph || modifiedValue is not CrdtGraph modifiedGraph) return;

        foreach (var vertex in modifiedGraph.Vertices.Except(originalGraph.Vertices))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new GraphVertexPayload(vertex), changeTimestamp, clock));
        }

        foreach (var vertex in originalGraph.Vertices.Except(modifiedGraph.Vertices))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new GraphVertexPayload(vertex), changeTimestamp, clock));
        }

        foreach (var edge in modifiedGraph.Edges.Except(originalGraph.Edges))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new GraphEdgePayload(edge), changeTimestamp, clock));
        }

        foreach (var edge in originalGraph.Edges.Except(modifiedGraph.Edges))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new GraphEdgePayload(edge), changeTimestamp, clock));
        }
    }

    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        var (_, _, path, _, intent, changeTimestamp, clock) = context;

        return intent switch
        {
            AddVertexIntent addVertex => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new GraphVertexPayload(addVertex.Vertex), changeTimestamp, clock),
            RemoveVertexIntent removeVertex => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new GraphVertexPayload(removeVertex.Vertex), changeTimestamp, clock),
            AddEdgeIntent addEdge => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new GraphEdgePayload(addEdge.Edge), changeTimestamp, clock),
            RemoveEdgeIntent removeEdge => new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new GraphEdgePayload(removeEdge.Edge), changeTimestamp, clock),
            _ => throw new NotSupportedException($"Intent {intent.GetType().Name} is not supported by {nameof(TwoPhaseGraphStrategy)}.")
        };
    }

    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var graphObj = PocoPathHelper.GetValue(root, operation.JsonPath);
        if (graphObj is not CrdtGraph graph) return;

        if (!metadata.TwoPhaseGraphs.TryGetValue(operation.JsonPath, out var state))
        {
            var vertexComparer = comparerProvider.GetComparer(typeof(object));
            var edgeComparer = comparerProvider.GetComparer(typeof(Edge));

            state = new TwoPhaseGraphState(new HashSet<object>(vertexComparer), new HashSet<object>(vertexComparer),
                     new HashSet<object>(edgeComparer), new HashSet<object>(edgeComparer));
            metadata.TwoPhaseGraphs[operation.JsonPath] = state;
        }
        
        object? payload = operation.Value;
        if (payload is GraphVertexPayload vertexPayload)
        {
            if (operation.Type == OperationType.Upsert) 
            {
                if (!state.VertexTombstones.Contains(vertexPayload.Vertex) && state.VertexAdds.Add(vertexPayload.Vertex))
                {
                    graph.Vertices.Add(vertexPayload.Vertex);
                }
            }
            else 
            {
                if (state.VertexTombstones.Add(vertexPayload.Vertex))
                {
                    graph.Vertices.Remove(vertexPayload.Vertex);
                }
            }
        }
        else if (payload is GraphEdgePayload edgePayload && edgePayload.Edge is Edge edge)
        {
            if (operation.Type == OperationType.Upsert) 
            {
                if (!state.EdgeTombstones.Contains(edgePayload.Edge) && state.EdgeAdds.Add(edgePayload.Edge))
                {
                    graph.Edges.Add(edge);
                }
            }
            else 
            {
                if (state.EdgeTombstones.Add(edgePayload.Edge))
                {
                    graph.Edges.Remove(edge);
                }
            }
        }
    }
}