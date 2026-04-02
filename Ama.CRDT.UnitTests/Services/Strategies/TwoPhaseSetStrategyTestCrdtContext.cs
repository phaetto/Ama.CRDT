namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for TwoPhaseSetStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtSerializable(typeof(TwoPhaseSetTestModel))]
[CrdtSerializable(typeof(List<string>))]
internal partial class TwoPhaseSetStrategyTestCrdtContext : CrdtContext
{
}