namespace Ama.CRDT.Models;

/// <summary>
/// Represents a collection of CRDT operations that, when applied, transform one document state into another.
/// </summary>
/// <param name="Operations">A read-only list of the <see cref="CrdtOperation"/>s in this patch.</param>
public readonly record struct CrdtPatch(IReadOnlyList<CrdtOperation> Operations) : IEquatable<CrdtPatch>
{
    /// <summary>
    /// The logical key for the document being patched, used for partitioning.
    /// This must be populated when applying patches to a partitioned document.
    /// </summary>
    public object? LogicalKey { get; init; }

    /// <inheritdoc />
    public bool Equals(CrdtPatch other)
    {
        if (!Equals(LogicalKey, other.LogicalKey))
        {
            return false;
        }
        if (ReferenceEquals(Operations, other.Operations)) return true;
        if (Operations is null || other.Operations is null) return false;
        return Operations.SequenceEqual(other.Operations);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(LogicalKey);
        if (Operations is not null)
        {
            foreach (var op in Operations)
            {
                hashCode.Add(op);
            }
        }
        return hashCode.ToHashCode();
    }
}