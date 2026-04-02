namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for TwoPhaseGraphStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtSerializable(typeof(TwoPhaseGraphTestModel))]
internal partial class TwoPhaseGraphStrategyTestCrdtContext : CrdtContext
{
}