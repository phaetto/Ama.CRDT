namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an Observed-Remove Set (OR-Set) add operation.
/// It bundles the value with a unique tag to identify this specific instance of the value.
/// </summary>
/// <param name="Value">The element being added to the set.</param>
/// <param name="Tag">A unique identifier (typically a GUID) for this instance of the element.</param>
public readonly record struct OrSetAddItem(object Value, Guid Tag);

/// <summary>
/// Represents the payload for an Observed-Remove Set (OR-Set) remove operation.
/// It bundles the value with the set of tags that identify the instances to be removed.
/// </summary>
/// <param name="Value">The element being removed from the set.</param>
/// <param name="Tags">The set of unique tags corresponding to the element instances to be removed.</param>
public readonly record struct OrSetRemoveItem(object Value, ISet<Guid> Tags) : IEquatable<OrSetRemoveItem>
{
    /// <inheritdoc />
    public bool Equals(OrSetRemoveItem other)
    {
        if (!EqualityComparer<object>.Default.Equals(Value, other.Value)) return false;
        if (ReferenceEquals(Tags, other.Tags)) return true;
        if (Tags is null || other.Tags is null) return false;
        return Tags.SetEquals(other.Tags);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Value);
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