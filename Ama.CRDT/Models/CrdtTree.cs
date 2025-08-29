namespace Ama.CRDT.Models;

/// <summary>
/// Represents a replicated tree data structure where nodes can be added, removed, and moved concurrently.
/// </summary>
public sealed record CrdtTree : IEquatable<CrdtTree>
{
    /// <summary>
    /// Gets or sets the dictionary of nodes in the tree, keyed by their unique identifier.
    /// </summary>
    public IDictionary<object, TreeNode> Nodes { get; set; } = new Dictionary<object, TreeNode>();

    /// <inheritdoc />
    public bool Equals(CrdtTree? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Nodes.Count != other.Nodes.Count)
        {
            return false;
        }

        foreach (var kvp in Nodes)
        {
            if (!other.Nodes.TryGetValue(kvp.Key, out var otherValue) || !Equals(kvp.Value, otherValue))
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int nodesHash = 0;
        foreach (var (key, value) in Nodes)
        {
            nodesHash ^= HashCode.Combine(key, value);
        }
        return nodesHash;
    }
}