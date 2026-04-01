namespace Ama.CRDT.ShowCase.CollaborativeEditing.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Attributes.Strategies;

public sealed class SharedDocument : IEquatable<SharedDocument>
{
    /// <summary>
    /// Represents the text lines of our document as an ordered list.
    /// The RgaStrategy (Replicated Growable Array) is the industry standard for 
    /// collaborative sequential data like text. We will use Index-based Intents 
    /// to update it precisely without calculating full-document differences.
    /// </summary>
    [CrdtRgaStrategy]
    public IList<string> Lines { get; set; } = new List<string>();

    public bool Equals(SharedDocument? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Lines.SequenceEqual(other.Lines);
    }

    public override bool Equals(object? obj) => Equals(obj as SharedDocument);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var line in Lines)
        {
            hash.Add(line);
        }
        return hash.ToHashCode();
    }
}