namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

public sealed record Comment(Guid Id, string Author, string Text, DateTimeOffset Timestamp);