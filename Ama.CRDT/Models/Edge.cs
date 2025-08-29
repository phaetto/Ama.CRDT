namespace Ama.CRDT.Models;

/// <summary>
/// Represents an edge in a graph, connecting two vertices and holding associated data.
/// </summary>
/// <param name="Source">The source vertex of the edge.</param>
/// <param name="Target">The target vertex of the edge.</param>
/// <param name="Data">The data associated with the edge.</param>
public readonly record struct Edge(object Source, object Target, object? Data);