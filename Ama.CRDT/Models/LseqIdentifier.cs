namespace Ama.CRDT.Models;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

/// <summary>
/// Represents a dense, ordered identifier for an element in an LSEQ sequence.
/// The identifier is a path in a conceptual, infinitely-branching tree, ensuring that
/// a new identifier can always be generated between any two existing identifiers.
/// </summary>
/// <param name="Path">The list of path components that form this identifier.</param>
public readonly record struct LseqIdentifier(ImmutableList<LseqPathSegment> Path) : IComparable<LseqIdentifier>, IComparable, IEquatable<LseqIdentifier>
{
    /// <inheritdoc />
    public int CompareTo(LseqIdentifier other)
    {
        var p1 = Path ?? ImmutableList<LseqPathSegment>.Empty;
        var p2 = other.Path ?? ImmutableList<LseqPathSegment>.Empty;

        var minLength = Math.Min(p1.Count, p2.Count);
        for (var i = 0; i < minLength; i++)
        {
            var segment1 = p1[i];
            var segment2 = p2[i];

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

        return p1.Count.CompareTo(p2.Count);
    }

    /// <inheritdoc />
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is LseqIdentifier other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(LseqIdentifier)}");
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var p = Path ?? ImmutableList<LseqPathSegment>.Empty;
        var sb = new StringBuilder();
        foreach (var segment in p)
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