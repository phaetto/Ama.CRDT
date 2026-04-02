namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for MinWinsMapStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtSerializable(typeof(MinWinsMapStrategyTests.TestModel))]
[CrdtSerializable(typeof(Dictionary<string, int>))]
[CrdtSerializable(typeof(KeyValuePair<object, object?>))]
internal partial class MinWinsMapStrategyTestCrdtContext : CrdtContext
{
}