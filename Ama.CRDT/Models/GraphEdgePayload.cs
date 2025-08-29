namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an operation that adds an edge to a graph.
/// </summary>
/// <param name="Edge">The edge being added.</param>
public readonly record struct GraphEdgePayload(Edge Edge);