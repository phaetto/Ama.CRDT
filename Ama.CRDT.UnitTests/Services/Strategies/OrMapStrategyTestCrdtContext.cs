namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for OrMapStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtSerializable(typeof(OrMapStrategyTests.TestModel))]
[CrdtSerializable(typeof(Dictionary<string, int>))]
internal partial class OrMapStrategyTestCrdtContext : CrdtContext
{
}