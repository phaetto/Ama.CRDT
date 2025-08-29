namespace Ama.CRDT.Models;

using System.Collections.Immutable;
using System.Text;

/// <summary>
/// Represents a dense, ordered identifier for an element in an LSEQ sequence.
/// The identifier is a path in a conceptual, infinitely-branching tree, ensuring that
/// a new identifier can always be generated between any two existing identifiers.
/// </summary>
/// <param name="Path">The list of path components that form this identifier.</param>
public readonly record struct LseqIdentifier(ImmutableList<LseqPathSegment> Path) : IComparable<LseqIdentifier>, IEquatable<LseqIdentifier>
{
    /// <inheritdoc />
    public int CompareTo(LseqIdentifier other)
    {
        var minLength = Math.Min(Path.Count, other.Path.Count);
        for (var i = 0; i < minLength; i++)
        {
            var segment1 = Path[i];
            var segment2 = other.Path[i];

            if (segment1.Position != segment2.Position)
            {
                return segment1.Position.CompareTo(segment2.Position);
            }

            // If positions are equal, the replica ID is used as a tie-breaker.
            var replicaComparison = string.Compare(segment1.ReplicaId, segment2.ReplicaId, StringComparison.Ordinal);
            if (replicaComparison != 0)
            {
                return replicaComparison;
            }
        }

        return Path.Count.CompareTo(other.Path.Count);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var segment in Path)
        {
            sb.Append($"({segment.Position},{segment.ReplicaId})-");
        }
        return sb.ToString().TrimEnd('-');
    }

    /// <inheritdoc />
    public bool Equals(LseqIdentifier other)
    {
        if (Path is null && other.Path is null) return true;
        if (Path is null || other.Path is null) return false;
        return Path.SequenceEqual(other.Path);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (Path is null) return 0;
        var hashCode = new HashCode();
        foreach (var segment in Path)
        {
            hashCode.Add(segment);
        }
        return hashCode.ToHashCode();
    }
}