namespace Ama.CRDT.Models.Intents;

using Ama.CRDT.Models;

/// <summary>
/// Represents the intent to explicitly add an edge to a graph.
/// </summary>
/// <param name="Edge">The edge to add.</param>
public readonly record struct AddEdgeIntent(Edge Edge) : IOperationIntent;