namespace Ama.CRDT.ShowCase.CollaborativeEditing.Models;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes.Strategies;

public sealed class SharedDocument
{
    /// <summary>
    /// Represents the text lines of our document. 
    /// The ArrayLcsStrategy will handle insertion, deletion, and convergence of lines conflict-free.
    /// </summary>
    [CrdtArrayLcsStrategy]
    public IList<string> Lines { get; set; } = new List<string>();
}