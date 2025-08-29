namespace Ama.CRDT.Models;

/// <summary>
/// Represents the state for an Observed-Remove Set (OR-Set) or OR-Map.
/// </summary>
/// <param name="Adds">A dictionary mapping added elements to a set of unique tags.</param>
/// <param name="Removes">A dictionary mapping removed elements to a set of unique tags.</param>
public sealed record OrSetState(IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) : IEquatable<OrSetState>
{
    private static bool DictionaryOfSetsEquals(IDictionary<object, ISet<Guid>> left, IDictionary<object, ISet<Guid>> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue)) return false;
            if (ReferenceEquals(value, rightValue)) continue;
            if (value is null || rightValue is null) return false;
            if (!value.SetEquals(rightValue)) return false;
        }
        return true;
    }

    private static int GetDictionaryOfSetsHashCode(IDictionary<object, ISet<Guid>> dict)
    {
        if (dict is null) return 0;
        int hash = 0;
        foreach (var (key, value) in dict)
        {
            int setHash = 0;
            if (value is not null)
            {
                foreach (var item in value.OrderBy(t => t))
                {
                    setHash ^= item.GetHashCode();
                }
            }
            hash ^= HashCode.Combine(key, setHash);
        }
        return hash;
    }

    /// <inheritdoc />
    public bool Equals(OrSetState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return DictionaryOfSetsEquals(Adds, other.Adds) && DictionaryOfSetsEquals(Removes, other.Removes);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetDictionaryOfSetsHashCode(Adds), GetDictionaryOfSetsHashCode(Removes));
    }
}