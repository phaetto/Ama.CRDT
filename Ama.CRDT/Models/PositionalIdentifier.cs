namespace Ama.CRDT.Models;

using System;
using System.Globalization;

/// <summary>
/// Represents a stable positional identifier for an element in an ordered collection,
/// used by the ArrayLcsStrategy. It consists of a fractional position string and the ID of the
/// operation that created it, which is used for tie-breaking concurrent insertions at the same position.
/// </summary>
/// <param name="Position">A string representation of a fractional number indicating the element's position.</param>
/// <param name="OperationId">The unique ID of the operation that inserted this element, used as a tie-breaker.</param>
public readonly record struct PositionalIdentifier(string Position, Guid OperationId) : IComparable<PositionalIdentifier>
{
    /// <summary>
    /// Compares this identifier to another, first by position and then by OperationId as a tie-breaker.
    /// </summary>
    /// <param name="other">The other <see cref="PositionalIdentifier"/> to compare against.</param>
    /// <returns>An integer indicating the relative order of the two identifiers.</returns>
    public int CompareTo(PositionalIdentifier other)
    {
        var positionComparison = decimal.Parse(Position, CultureInfo.InvariantCulture).CompareTo(decimal.Parse(other.Position, CultureInfo.InvariantCulture));
        if (positionComparison != 0)
        {
            return positionComparison;
        }

        return OperationId.CompareTo(other.OperationId);
    }
}