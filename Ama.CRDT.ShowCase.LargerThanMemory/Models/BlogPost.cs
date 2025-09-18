namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using Ama.CRDT.Attributes;

[PartitionKey(nameof(Id))]
public sealed class BlogPost
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    [CrdtArrayLcsStrategy]
    public IList<Comment> Comments { get; set; } = [];
}