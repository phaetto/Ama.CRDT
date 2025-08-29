namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an operation that removes a node from a replicated tree.
/// </summary>
/// <param name="NodeId">The unique identifier of the node to remove.</param>
/// <param name="Tags">The set of tags associated with the node's additions that are being removed.</param>
public readonly record struct TreeRemoveNodePayload(object NodeId, ISet<Guid> Tags) : IEquatable<TreeRemoveNodePayload>
{
    /// <inheritdoc />
    public bool Equals(TreeRemoveNodePayload other)
    {
        if (!EqualityComparer<object>.Default.Equals(NodeId, other.NodeId)) return false;
        if (ReferenceEquals(Tags, other.Tags)) return true;
        if (Tags is null || other.Tags is null) return false;
        return Tags.SetEquals(other.Tags);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(NodeId);
        if (Tags is not null)
        {
            int setHash = 0;
            foreach (var tag in Tags.OrderBy(t => t))
            {
                setHash ^= tag.GetHashCode();
            }
            hashCode.Add(setHash);
        }
        return hashCode.ToHashCode();
    }
}