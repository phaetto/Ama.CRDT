namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly add a vertex to a graph.
/// </summary>
/// <param name="Vertex">The vertex to add.</param>
public readonly record struct AddVertexIntent(object Vertex) : IOperationIntent;