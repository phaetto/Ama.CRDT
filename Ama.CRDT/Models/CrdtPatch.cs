namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a collection of CRDT operations that, when applied, transform one document state into another.
/// </summary>
/// <param name="Operations">A read-only list of the <see cref="CrdtOperation"/>s in this patch.</param>
public readonly record struct CrdtPatch(IReadOnlyList<CrdtOperation> Operations) : IEquatable<CrdtPatch>
{
    /// <inheritdoc />
    public bool Equals(CrdtPatch other)
    {
        if (ReferenceEquals(Operations, other.Operations)) return true;
        if (Operations is null || other.Operations is null) return false;
        return Operations.SequenceEqual(other.Operations);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
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