namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;

[CrdtSupportedType(typeof(CrdtGraph))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class GraphStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (_, operations, path, _, originalValue, modifiedValue, _, _, _, changeTimestamp) = context;

        if (originalValue is not CrdtGraph originalGraph || modifiedValue is not CrdtGraph modifiedGraph)
        {
            return;
        }
        
        foreach (var vertex in modifiedGraph.Vertices.Except(originalGraph.Vertices))
        {
            var payload = new GraphVertexPayload(vertex);
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp));
        }

        foreach (var edge in modifiedGraph.Edges.Except(originalGraph.Edges))
        {
            var payload = new GraphEdgePayload(edge);
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp));
        }
    }

    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, _, operation) = context;
        
        var graphObj = PocoPathHelper.GetValue(root, operation.JsonPath);
        if (graphObj is not CrdtGraph graph) return;

        if (operation.Value is GraphVertexPayload vertexPayload)
        {
            graph.Vertices.Add(vertexPayload.Vertex);
        }
        else if (operation.Value is GraphEdgePayload edgePayload)
        {
            graph.Edges.Add(edgePayload.Edge);
        }
    }
}