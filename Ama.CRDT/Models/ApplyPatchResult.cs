namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the result of applying a CRDT patch.
/// </summary>
/// <typeparam name="T">The type of the document.</typeparam>
public readonly record struct ApplyPatchResult<T>(
    T Document,
    IReadOnlyList<UnappliedOperation> UnappliedOperations
) : IEquatable<ApplyPatchResult<T>> where T : class
{
    /// <inheritdoc />
    public bool Equals(ApplyPatchResult<T> other)
    {
        if (!EqualityComparer<T>.Default.Equals(Document, other.Document))
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
        
        for (int i = 0; i < UnappliedOperations.Count; i++)
        {
            hash.Add(UnappliedOperations[i]);
        }
        
        return hash.ToHashCode();
    }
}