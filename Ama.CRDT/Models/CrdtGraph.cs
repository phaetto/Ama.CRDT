namespace Ama.CRDT.Models;

/// <summary>
/// Represents an edge in a graph, connecting two vertices and holding associated data.
/// </summary>
/// <param name="Source">The source vertex of the edge.</param>
/// <param name="Target">The target vertex of the edge.</param>
/// <param name="Data">The data associated with the edge.</param>
public readonly record struct Edge(object Source, object Target, object? Data);

/// <summary>
/// Represents a graph data structure with a set of vertices and edges.
/// </summary>
public sealed class CrdtGraph
{
    /// <summary>
    /// Gets or sets the set of vertices in the graph.
    /// </summary>
    public ISet<object> Vertices { get; set; } = new HashSet<object>();

    /// <summary>
    /// Gets or sets the set of edges in the graph.
    /// </summary>
    public ISet<Edge> Edges { get; set; } = new HashSet<Edge>();
}