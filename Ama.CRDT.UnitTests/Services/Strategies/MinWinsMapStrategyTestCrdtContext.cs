namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for MinWinsMapStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtAotType(typeof(MinWinsMapStrategyTests.TestModel))]
[CrdtAotType(typeof(Dictionary<string, int>))]
[CrdtAotType(typeof(KeyValuePair<object, object?>))]
internal partial class MinWinsMapStrategyTestCrdtAotContext : CrdtAotContext
{
}