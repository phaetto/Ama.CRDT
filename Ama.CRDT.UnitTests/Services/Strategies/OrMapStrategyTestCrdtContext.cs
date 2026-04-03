namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for OrMapStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtAotType(typeof(OrMapStrategyTests.TestModel))]
[CrdtAotType(typeof(Dictionary<string, int>))]
internal partial class OrMapStrategyTestCrdtAotContext : CrdtAotContext
{
}