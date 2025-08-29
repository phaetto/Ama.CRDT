namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;

[CrdtSupportedType(typeof(CrdtGraph))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class TwoPhaseGraphStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (_, operations, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp) = context;

        if (originalValue is not CrdtGraph originalGraph || modifiedValue is not CrdtGraph modifiedGraph) return;

        foreach (var vertex in modifiedGraph.Vertices.Except(originalGraph.Vertices))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new GraphVertexPayload(vertex), changeTimestamp));
        }

        foreach (var vertex in originalGraph.Vertices.Except(modifiedGraph.Vertices))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new GraphVertexPayload(vertex), changeTimestamp));
        }

        foreach (var edge in modifiedGraph.Edges.Except(originalGraph.Edges))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, new GraphEdgePayload(edge), changeTimestamp));
        }

        foreach (var edge in originalGraph.Edges.Except(modifiedGraph.Edges))
        {
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, new GraphEdgePayload(edge), changeTimestamp));
        }
    }

    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var graphObj = PocoPathHelper.GetValue(root, operation.JsonPath);
        if (graphObj is not CrdtGraph graph) return;

        if (!metadata.TwoPhaseGraphs.TryGetValue(operation.JsonPath, out var state))
        {
            IEqualityComparer<object> vertexComparer = EqualityComparer<object>.Default;
            var sampleVertex = graph.Vertices.FirstOrDefault();
            if (sampleVertex is not null)
            {
                vertexComparer = comparerProvider.GetComparer(sampleVertex.GetType());
            }
            
            var edgeComparer = comparerProvider.GetComparer(typeof(Edge));

            state = (new HashSet<object>(vertexComparer), new HashSet<object>(vertexComparer),
                     new HashSet<object>(edgeComparer), new HashSet<object>(edgeComparer));
            metadata.TwoPhaseGraphs[operation.JsonPath] = state;
        }
        
        object? payload = operation.Value;
        if (payload is GraphVertexPayload vertexPayload)
        {
            if (operation.Type == OperationType.Upsert) 
                state.VertexAdds.Add(vertexPayload.Vertex);
            else 
                state.VertexTombstones.Add(vertexPayload.Vertex);
        }
        else if (payload is GraphEdgePayload edgePayload)
        {
            if (operation.Type == OperationType.Upsert) 
                state.EdgeAdds.Add(edgePayload.Edge);
            else 
                state.EdgeTombstones.Add(edgePayload.Edge);
        }
        
        ReconstructGraph(graph, state);
    }

    private static void ReconstructGraph(CrdtGraph graph, (ISet<object> VertexAdds, ISet<object> VertexTombstones, ISet<object> EdgeAdds, ISet<object> EdgeTombstones) state)
    {
        graph.Vertices.Clear();
        foreach (var vertex in state.VertexAdds)
        {
            if (!state.VertexTombstones.Contains(vertex))
            {
                graph.Vertices.Add(vertex);
            }
        }
        
        graph.Edges.Clear();
        foreach (var edge in state.EdgeAdds)
        {
            if (!state.EdgeTombstones.Contains(edge) && edge is Edge typedEdge)
            {
                graph.Edges.Add(typedEdge);
            }
        }
    }
}