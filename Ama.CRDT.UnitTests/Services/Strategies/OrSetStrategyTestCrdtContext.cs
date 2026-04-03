namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for OrSetStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtAotType(typeof(OrSetStrategyTests.TestModel))]
[CrdtAotType(typeof(List<string>))]
internal partial class OrSetStrategyTestCrdtAotContext : CrdtAotContext
{
}