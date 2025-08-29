namespace Ama.CRDT.Models;

/// <summary>
/// Represents the state for a Two-Phase Graph (2P-Graph).
/// </summary>
/// <param name="VertexAdds">A set of added vertices.</param>
/// <param name="VertexTombstones">A set of removed vertices (tombstones).</param>
/// <param name="EdgeAdds">A set of added edges.</param>
/// <param name="EdgeTombstones">A set of removed edges (tombstones).</param>
public sealed record TwoPhaseGraphState(ISet<object> VertexAdds, ISet<object> VertexTombstones, ISet<object> EdgeAdds, ISet<object> EdgeTombstones) : IEquatable<TwoPhaseGraphState>
{
    private static bool SetEquals(ISet<object> left, ISet<object> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.SetEquals(right);
    }

    private static int GetSetHashCode(ISet<object> set)
    {
        if (set is null) return 0;
        int hash = 0;
        foreach (var item in set)
        {
            hash ^= item?.GetHashCode() ?? 0;
        }
        return hash;
    }

    /// <inheritdoc />
    public bool Equals(TwoPhaseGraphState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return SetEquals(VertexAdds, other.VertexAdds) &&
               SetEquals(VertexTombstones, other.VertexTombstones) &&
               SetEquals(EdgeAdds, other.EdgeAdds) &&
               SetEquals(EdgeTombstones, other.EdgeTombstones);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            GetSetHashCode(VertexAdds),
            GetSetHashCode(VertexTombstones),
            GetSetHashCode(EdgeAdds),
            GetSetHashCode(EdgeTombstones)
        );
    }
}