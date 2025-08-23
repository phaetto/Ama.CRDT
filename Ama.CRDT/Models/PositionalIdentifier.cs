namespace Ama.CRDT.Models;

using System;
using System.Globalization;

/// <summary>
/// Represents a stable positional identifier for an element in an ordered list.
/// It consists of a fractional position string and the ID of the operation that created it, used for tie-breaking.
/// </summary>
public readonly record struct PositionalIdentifier(string Position, Guid OperationId) : IComparable<PositionalIdentifier>
{
    /// <summary>
    /// Compares this identifier to another, first by position and then by OperationId as a tie-breaker.
    /// </summary>
    public int CompareTo(PositionalIdentifier other)
    {
        // Using decimal for comparison to correctly handle fractional values like "1.5" vs "2".
        var positionComparison = decimal.Parse(Position, CultureInfo.InvariantCulture).CompareTo(decimal.Parse(other.Position, CultureInfo.InvariantCulture));
        if (positionComparison != 0)
        {
            return positionComparison;
        }

        // If positions are identical, it's a concurrent insert. Use OperationId as a deterministic tie-breaker.
        return OperationId.CompareTo(other.OperationId);
    }
}