namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.GarbageCollection;
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
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
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

    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var graphObj = PocoPathHelper.GetValue(root, operation.JsonPath, aotContexts);
        if (graphObj is not CrdtGraph graph)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        if (!metadata.States.TryGetValue(operation.JsonPath, out var baseState) || baseState is not TwoPhaseGraphState state)
        {
            var vertexComparer = comparerProvider.GetComparer(typeof(object));
            var edgeComparer = comparerProvider.GetComparer(typeof(Edge));

            state = new TwoPhaseGraphState(
                new HashSet<object>(vertexComparer), 
                new Dictionary<object, CausalTimestamp>(vertexComparer),
                new HashSet<object>(edgeComparer), 
                new Dictionary<object, CausalTimestamp>(edgeComparer));
            metadata.States[operation.JsonPath] = state;
        }
        
        object? payload = operation.Value;
        if (payload is GraphVertexPayload vertexPayload)
        {
            if (operation.Type == OperationType.Upsert) 
            {
                if (!state.VertexTombstones.ContainsKey(vertexPayload.Vertex) && state.VertexAdds.Add(vertexPayload.Vertex))
                {
                    graph.Vertices.Add(vertexPayload.Vertex);
                }
            }
            else if (operation.Type == OperationType.Remove)
            {
                if (!state.VertexTombstones.TryGetValue(vertexPayload.Vertex, out var existingTs) || operation.Timestamp.CompareTo(existingTs.Timestamp) > 0)
                {
                    state.VertexTombstones[vertexPayload.Vertex] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
                    graph.Vertices.Remove(vertexPayload.Vertex);
                }
            }
            else
            {
                return CrdtOperationStatus.StrategyApplicationFailed;
            }
        }
        else if (payload is GraphEdgePayload edgePayload && edgePayload.Edge is Edge edge)
        {
            if (operation.Type == OperationType.Upsert) 
            {
                if (!state.EdgeTombstones.ContainsKey(edgePayload.Edge) && state.EdgeAdds.Add(edgePayload.Edge))
                {
                    graph.Edges.Add(edge);
                }
            }
            else if (operation.Type == OperationType.Remove)
            {
                if (!state.EdgeTombstones.TryGetValue(edgePayload.Edge, out var existingTs) || operation.Timestamp.CompareTo(existingTs.Timestamp) > 0)
                {
                    state.EdgeTombstones[edgePayload.Edge] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
                    graph.Edges.Remove(edge);
                }
            }
            else
            {
                return CrdtOperationStatus.StrategyApplicationFailed;
            }
        }
        else
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        return CrdtOperationStatus.Success;
    }

    public void Compact(CompactionContext context)
    {
        if (context.Metadata.States.TryGetValue(context.PropertyPath, out var baseState) && baseState is TwoPhaseGraphState state)
        {
            var verticesToRemove = new List<object>();
            foreach (var kvp in state.VertexTombstones)
            {
                var candidate = new CompactionCandidate(kvp.Value.Timestamp, kvp.Value.ReplicaId, kvp.Value.Clock);
                if (context.Policy.IsSafeToCompact(candidate))
                {
                    verticesToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var v in verticesToRemove)
            {
                state.VertexTombstones.Remove(v);
                state.VertexAdds.Remove(v);
            }

            var edgesToRemove = new List<object>();
            foreach (var kvp in state.EdgeTombstones)
            {
                var candidate = new CompactionCandidate(kvp.Value.Timestamp, kvp.Value.ReplicaId, kvp.Value.Clock);
                if (context.Policy.IsSafeToCompact(candidate))
                {
                    edgesToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var e in edgesToRemove)
            {
                state.EdgeTombstones.Remove(e);
                state.EdgeAdds.Remove(e);
            }
        }
    }

    public void MergeAsStateCrdt(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo property)
    {
        var path = $"$.{property.JsonName}";

        if (!meta2.States.TryGetValue(path, out var baseState2) || baseState2 is not TwoPhaseGraphState state2)
        {
            return;
        }

        if (!meta1.States.TryGetValue(path, out var baseState1) || baseState1 is not TwoPhaseGraphState state1)
        {
            var vertexComparer = comparerProvider.GetComparer(typeof(object));
            var edgeComparer = comparerProvider.GetComparer(typeof(Edge));

            state1 = new TwoPhaseGraphState(
                new HashSet<object>(vertexComparer), 
                new Dictionary<object, CausalTimestamp>(vertexComparer),
                new HashSet<object>(edgeComparer), 
                new Dictionary<object, CausalTimestamp>(edgeComparer));
            meta1.States[path] = state1;
        }

        foreach (var v in state2.VertexAdds)
        {
            state1.VertexAdds.Add(v);
        }

        foreach (var kvp in state2.VertexTombstones)
        {
            if (!state1.VertexTombstones.TryGetValue(kvp.Key, out var existing) || kvp.Value.Timestamp.CompareTo(existing.Timestamp) > 0)
            {
                state1.VertexTombstones[kvp.Key] = kvp.Value;
            }
        }

        foreach (var e in state2.EdgeAdds)
        {
            state1.EdgeAdds.Add(e);
        }

        foreach (var kvp in state2.EdgeTombstones)
        {
            if (!state1.EdgeTombstones.TryGetValue(kvp.Key, out var existing) || kvp.Value.Timestamp.CompareTo(existing.Timestamp) > 0)
            {
                state1.EdgeTombstones[kvp.Key] = kvp.Value;
            }
        }

        var graphObj = PocoPathHelper.GetValue(data1, path, aotContexts);
        if (graphObj is not CrdtGraph graph)
        {
            graph = new CrdtGraph();
            PocoPathHelper.SetValue(data1, path, graph, aotContexts);
        }

        graph.Vertices.Clear();
        foreach (var v in state1.VertexAdds)
        {
            if (!state1.VertexTombstones.ContainsKey(v))
            {
                graph.Vertices.Add(v);
            }
        }

        graph.Edges.Clear();
        foreach (var e in state1.EdgeAdds)
        {
            if (!state1.EdgeTombstones.ContainsKey(e))
            {
                graph.Edges.Add((Edge)e);
            }
        }
    }
}