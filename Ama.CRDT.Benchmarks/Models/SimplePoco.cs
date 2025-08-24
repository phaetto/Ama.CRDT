using Ama.CRDT.Attributes;

namespace Ama.CRDT.Benchmarks.Models;

public sealed class SimplePoco
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    [CrdtCounter]
    public int Score { get; set; }
}