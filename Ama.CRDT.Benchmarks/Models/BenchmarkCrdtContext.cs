namespace Ama.CRDT.Benchmarks.Models;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

[CrdtSerializable(typeof(SimplePoco))]
[CrdtSerializable(typeof(ComplexPoco))]
[CrdtSerializable(typeof(Details))]
[CrdtSerializable(typeof(Tag))]
[CrdtSerializable(typeof(StrategyPoco))]
[CrdtSerializable(typeof(PrioItem))]
public partial class BenchmarkCrdtContext : CrdtContext
{
}