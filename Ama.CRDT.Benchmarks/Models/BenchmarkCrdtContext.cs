namespace Ama.CRDT.Benchmarks.Models;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;

[CrdtSerializable(typeof(SimplePoco))]
[CrdtSerializable(typeof(ComplexPoco))]
[CrdtSerializable(typeof(Details))]
[CrdtSerializable(typeof(Tag))]
[CrdtSerializable(typeof(StrategyPoco))]
[CrdtSerializable(typeof(PrioItem))]
[CrdtSerializable(typeof(List<Tag>))]
[CrdtSerializable(typeof(List<string>))]
[CrdtSerializable(typeof(string?[]))]
[CrdtSerializable(typeof(Dictionary<string, List<string>>))]
[CrdtSerializable(typeof(List<PrioItem>))]
[CrdtSerializable(typeof(Dictionary<string, int>))]
[CrdtSerializable(typeof(Dictionary<string, string>))]
public partial class BenchmarkCrdtContext : CrdtContext
{
}