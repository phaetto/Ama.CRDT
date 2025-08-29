namespace Ama.CRDT.Models;

/// <summary>
/// Represents a graph data structure with a set of vertices and edges.
/// </summary>
public sealed record CrdtGraph : IEquatable<CrdtGraph>
{
    /// <summary>
    /// Gets or sets the set of vertices in the graph.
    /// </summary>
    public ISet<object> Vertices { get; set; } = new HashSet<object>();

    /// <summary>
    /// Gets or sets the set of edges in the graph.
    /// </summary>
    public ISet<Edge> Edges { get; set; } = new HashSet<Edge>();

    /// <inheritdoc />
    public bool Equals(CrdtGraph? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Vertices.SetEquals(other.Vertices) && Edges.SetEquals(other.Edges);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        int verticesHash = 0;
        foreach (var vertex in Vertices.OrderBy(v => v.GetHashCode()))
        {
            verticesHash ^= vertex?.GetHashCode() ?? 0;
        }
        hashCode.Add(verticesHash);

        int edgesHash = 0;
        foreach (var edge in Edges.OrderBy(e => e.GetHashCode()))
        {
            edgesHash ^= edge.GetHashCode();
        }
        hashCode.Add(edgesHash);

        return hashCode.ToHashCode();
    }
}