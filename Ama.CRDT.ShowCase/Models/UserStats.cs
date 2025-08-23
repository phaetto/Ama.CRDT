namespace Ama.CRDT.ShowCase.Models;
using Ama.CRDT.Attributes;

public sealed class UserStats
{
    [CrdtCounter]
    public long ProcessedItemsCount { get; set; }

    [CrdtArrayLcsStrategy]
    public List<string> UniqueUserNames { get; set; } = [];

    [LwwStrategy]
    public string LastProcessedUserName { get; set; } = string.Empty;

    [LwwStrategy]
    public long LastProcessedTimestamp { get; set; }
}