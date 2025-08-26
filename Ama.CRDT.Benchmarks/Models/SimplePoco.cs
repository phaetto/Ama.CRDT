namespace Ama.CRDT.Benchmarks.Models;
using Ama.CRDT.Attributes;

public sealed class SimplePoco
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    [CrdtCounterStrategy]
    public int Score { get; set; }
}