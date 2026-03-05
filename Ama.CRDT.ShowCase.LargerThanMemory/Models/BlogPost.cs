namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using System.Collections.Generic;

[PartitionKey(nameof(Id))]
public sealed class BlogPost
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    [CrdtOrMapStrategy]
    public IDictionary<DateTimeOffset, Comment> Comments { get; set; } = new Dictionary<DateTimeOffset, Comment>();

    [CrdtArrayLcsStrategy]
    public IList<string> Tags { get; set; } = [];
}