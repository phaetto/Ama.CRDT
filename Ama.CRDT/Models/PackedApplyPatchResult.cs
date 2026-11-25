namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the result of applying a CRDT patch to a <see cref="PackedCrdtDocument{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the document.</typeparam>
public readonly record struct PackedApplyPatchResult<T>(
    PackedCrdtDocument<T> Document,
    IReadOnlyList<UnappliedOperation> UnappliedOperations
) : IEquatable<PackedApplyPatchResult<T>> where T : class
{
    /// <inheritdoc />
    public bool Equals(PackedApplyPatchResult<T> other)
    {
        if (!Document.Equals(other.Document))
        {
            return false;
        }

        if (UnappliedOperations is null && other.UnappliedOperations is null)
        {
            return true;
        }

        if (UnappliedOperations is null || other.UnappliedOperations is null)
        {
            return false;
        }

        if (UnappliedOperations.Count != other.UnappliedOperations.Count)
        {
            return false;
        }

        for (int i = 0; i < UnappliedOperations.Count; i++)
        {
            if (!UnappliedOperations[i].Equals(other.UnappliedOperations[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Document);

        if (UnappliedOperations is not null)
        {
            for (int i = 0; i < UnappliedOperations.Count; i++)
            {
                hash.Add(UnappliedOperations[i]);
            }
        }

        return hash.ToHashCode();
    }
}