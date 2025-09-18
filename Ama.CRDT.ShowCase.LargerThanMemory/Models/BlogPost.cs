namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using Ama.CRDT.Attributes;
using System.Collections.Generic;

[PartitionKey(nameof(Id))]
public sealed class BlogPost
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    // Lcs user PositionalIdentifier to track position, and this in the end sorts with their Guid
    // TODO: Lcs might not be partition friendly.
    //[CrdtArrayLcsStrategy]
    //public IList<Comment> Comments { get; set; } = [];

    [CrdtOrMapStrategy]
    public IDictionary<DateTimeOffset, Comment> Comments { get; set; } = new Dictionary<DateTimeOffset, Comment>();
}