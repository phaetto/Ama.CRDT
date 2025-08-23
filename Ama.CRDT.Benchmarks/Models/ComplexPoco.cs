using Ama.CRDT.Attributes;

namespace Ama.CRDT.Benchmarks.Models;

public sealed class ComplexPoco
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public Details Details { get; set; } = new();
    public List<Tag> Tags { get; set; } = [];

    [CrdtCounter]
    public long ViewCount { get; set; }
}

public sealed class Details
{
    public DateTime CreatedAt { get; set; }
    public string Author { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class Tag
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}