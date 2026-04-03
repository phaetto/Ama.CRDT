namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for StateMachineStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtAotType(typeof(StateMachineTestModel))]
internal partial class StateMachineStrategyTestCrdtAotContext : CrdtAotContext
{
}