namespace Ama.CRDT.ShowCase.LargerThanMemory.Models;

using System;

public sealed record Comment(Guid Id, string Author, string Text, DateTimeOffset CreatedAt);