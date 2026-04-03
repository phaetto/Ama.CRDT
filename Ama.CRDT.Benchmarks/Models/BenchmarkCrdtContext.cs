namespace Ama.CRDT.Benchmarks.Models;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

[CrdtAotType(typeof(SimplePoco))]
[CrdtAotType(typeof(ComplexPoco))]
[CrdtAotType(typeof(Details))]
[CrdtAotType(typeof(Tag))]
[CrdtAotType(typeof(StrategyPoco))]
[CrdtAotType(typeof(PrioItem))]
[CrdtAotType(typeof(List<Tag>))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(string?[]))]
[CrdtAotType(typeof(Dictionary<string, List<string>>))]
[CrdtAotType(typeof(List<PrioItem>))]
[CrdtAotType(typeof(Dictionary<string, int>))]
[CrdtAotType(typeof(Dictionary<string, string>))]
public partial class BenchmarkCrdtAotContext : CrdtAotContext
{
}