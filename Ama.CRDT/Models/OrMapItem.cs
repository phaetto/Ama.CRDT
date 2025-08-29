namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for an Observed-Remove Map (OR-Map) add or update operation.
/// It bundles the key, value, and a unique tag for the key's instance.
/// </summary>
/// <param name="Key">The key being added or updated in the map.</param>
/// <param name="Value">The value associated with the key.</param>
/// <param name="Tag">A unique identifier (typically a GUID) for this instance of the key.</param>
public readonly record struct OrMapAddItem(object Key, object? Value, Guid Tag);

/// <summary>
/// Represents the payload for an Observed-Remove Map (OR-Map) remove operation.
/// It bundles the key with the set of tags that identify the instances to be removed.
/// </summary>
/// <param name="Key">The key being removed from the map.</param>
/// <param name="Tags">The set of unique tags corresponding to the key instances to be removed.</param>
public readonly record struct OrMapRemoveItem(object Key, ISet<Guid> Tags) : IEquatable<OrMapRemoveItem>
{
    /// <inheritdoc />
    public bool Equals(OrMapRemoveItem other)
    {
        if (!EqualityComparer<object>.Default.Equals(Key, other.Key)) return false;
        if (ReferenceEquals(Tags, other.Tags)) return true;
        if (Tags is null || other.Tags is null) return false;
        return Tags.SetEquals(other.Tags);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Key);
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