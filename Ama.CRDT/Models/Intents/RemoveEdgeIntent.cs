namespace Ama.CRDT.Models.Intents;

using Ama.CRDT.Models;

/// <summary>
/// Represents the intent to explicitly remove an edge from a graph.
/// </summary>
/// <param name="Edge">The edge to remove.</param>
public readonly record struct RemoveEdgeIntent(Edge Edge) : IOperationIntent;