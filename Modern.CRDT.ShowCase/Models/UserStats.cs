namespace Modern.CRDT.ShowCase.Models;
using Modern.CRDT.Attributes;

public sealed class UserStats
{
    [CrdtCounter]
    public long ProcessedItemsCount { get; set; }

    [CrdtArrayLcsStrategy]
    public List<User> UniqueUsers { get; set; } = [];

    [LwwStrategy]
    public string LastProcessedUserName { get; set; } = string.Empty;

    [LwwStrategy]
    public long LastProcessedTimestamp { get; set; }
}