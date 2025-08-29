namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an operation that adds a vertex to a graph.
/// </summary>
/// <param name="Vertex">The vertex being added.</param>
public readonly record struct GraphVertexPayload(object Vertex);