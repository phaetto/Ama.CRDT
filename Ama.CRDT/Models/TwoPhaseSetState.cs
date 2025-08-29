namespace Ama.CRDT.Models;

/// <summary>
/// Represents the state for a Two-Phase Set (2P-Set).
/// </summary>
/// <param name="Adds">A set of added elements.</param>
/// <param name="Tomstones">A set of removed elements (tombstones).</param>
public sealed record TwoPhaseSetState(ISet<object> Adds, ISet<object> Tomstones) : IEquatable<TwoPhaseSetState>
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
    public bool Equals(TwoPhaseSetState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return SetEquals(Adds, other.Adds) && SetEquals(Tomstones, other.Tomstones);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetSetHashCode(Adds), GetSetHashCode(Tomstones));
    }
}