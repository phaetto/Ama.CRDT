namespace Ama.CRDT.Models;

using System.Collections.Immutable;
using System.Text;

/// <summary>
/// Represents a dense, ordered identifier for an element in an LSEQ sequence.
/// The identifier is a path in a conceptual, infinitely-branching tree, ensuring that
/// a new identifier can always be generated between any two existing identifiers.
/// </summary>
public readonly record struct LseqIdentifier : IComparable<LseqIdentifier>
{
    /// <summary>
    /// Gets the list of path components that form this identifier.
    /// </summary>
    public ImmutableList<(int Position, string ReplicaId)> Path { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LseqIdentifier"/> struct.
    /// </summary>
    /// <param name="path">The list of path components.</param>
    public LseqIdentifier(ImmutableList<(int Position, string ReplicaId)> path)
    {
        Path = path;
    }

    /// <inheritdoc />
    public int CompareTo(LseqIdentifier other)
    {
        var minLength = Math.Min(Path.Count, other.Path.Count);
        for (var i = 0; i < minLength; i++)
        {
            var (pos1, replica1) = Path[i];
            var (pos2, replica2) = other.Path[i];

            if (pos1 != pos2)
            {
                return pos1.CompareTo(pos2);
            }
            // If positions are equal, the replica ID is used as a tie-breaker.
            // This case should be rare in a correct implementation but is necessary for total ordering.
            var replicaComparison = string.Compare(replica1, replica2, StringComparison.Ordinal);
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
        foreach (var (pos, replica) in Path)
        {
            sb.Append($"({pos},{replica})-");
        }
        return sb.ToString().TrimEnd('-');
    }
}