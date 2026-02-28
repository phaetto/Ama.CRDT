namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly remove a vertex from a graph.
/// </summary>
/// <param name="Vertex">The vertex to remove.</param>
public readonly record struct RemoveVertexIntent(object Vertex) : IOperationIntent;