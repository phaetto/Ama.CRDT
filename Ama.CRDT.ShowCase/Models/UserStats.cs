namespace Ama.CRDT.ShowCase.Models;
using Ama.CRDT.Attributes;

public sealed class UserStats
{
    [CrdtGCounterStrategy]
    public long ProcessedItemsCount { get; set; }

    [CrdtLseqStrategy]
    public List<string> UniqueUserNames { get; set; } = [];

    [CrdtLwwStrategy]
    public string LastProcessedUserName { get; set; } = string.Empty;

    [CrdtLwwStrategy]
    public long LastProcessedTimestamp { get; set; }
}